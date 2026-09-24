# Lane 6: repo, release and harness (run `2026-09-23-opus`, baseline `b2e387db`)

Read-only. Playbook sections 6 (Dependencies and Migrations), 7 (DX and Tooling) and 8 (Docs, beyond
the linter). Builds on `baseline.md` (hook count and timings, tree weight, CHANGELOG churn, subject
misses) and seeds F1, F5, F7 (as corrected), F8. Cross-references lanes 1 to 4 and the June
re-triage instead of re-reporting them. All repo reads are at `b2e387db` (`git show`, `git ls-files`,
`git ls-tree`) unless a line says otherwise.

## Findings

### [DX-L6-01] Move the load-bearing commit gates into committed git hooks and a CI job on both branches: today they bind only Claude, and the one CI that exists has been red for 40 straight runs

- **Measurement**: gate inventory from `git show b2e387db:.claude/settings.json` (13 Bash-path registrations) and
  `git show b2e387db:.github/workflows/build.yml`, `doc-budget.yml` (every `tools/*.py` a workflow runs:
  `grep -oE "tools/[a-z_]+\.py"`). Subject misses: `git log --format='%h|%ad|%s' a4c6d7c3..b2e387db` minus the regex
  `^[a-z]+(\([^)]*\))?!?: v[0-9]+\.[0-9]+\.[0-9]+ - ` (10 of 95, matches baseline). GitHub state, read-only:
  `gh api repos/haterade22/TAOM/branches/<b>` (`protected:false` for both `bannerlord-1.5.x` and `bannerlord-1.4.5`),
  `gh api .../rulesets` (0), `gh api .../actions/runners` (`total_count` 0), `gh variable list` (empty),
  `gh run list --workflow "Build & Test" --limit 40` (40 of 40 `failure`, 2026-09-07 to 2026-09-15),
  `gh run view 35005853072 --log-failed`. Local git hooks: `git config --show-origin --get-all core.hooksPath`.
- **Evidence (who each gate binds)**:

  | Gate | Claude hook | Runs for an IDE, terminal or Codex commit? |
  |---|---|---|
  | Subject `type: vX.Y.Z - desc` + no AI trailer | `check-commit-subject-version.sh` (13,703 B) | **No** (no git hook, no CI step) |
  | CHANGELOG staged with `.claude/`, CLAUDE.md, AGENTS.md | `check-changelog-changed.sh` | **No** |
  | Nothing under `.claude/{skills,agents,rules,hooks}` untracked or ignored | `check-claude-files-tracked.sh` | **No** |
  | ModuleData refs, landless cultures, duplicate ids (22 codes) | `check-moduledata-validation.sh` | **No** (`validate_moduledata` is in no workflow, triage-B L571) |
  | Shield troop never lacks a drawable polearm | `check-polearm-shield-parity.sh` (PostToolUse Edit) | **No** |
  | Vendored native DLL links a static CRT | `check-native-dll-crt.sh` | Only on a `bannerlord-1.4.5` push (`build.yml:3-8,81-102`) |
  | Doc drift and ADR-011 budget | `check-doc-config-drift.sh` | **Yes**, `doc-budget.yml` on every branch |
  | Force push to a protected branch | `validate-push.sh` | **No**: GitHub has no protection or ruleset on either branch |
  | `--no-verify`, destructive git, broad `git add` | `block-no-verify.sh`, `block-dangerous-git.sh`, `block-broad-git-add.sh` | Claude-specific by nature (keep as Claude hooks) |

  The hooks say so themselves: the `check-commit-subject-version.sh` deny text ("this gate sees only the commits Claude
  runs"); `harness-facts.md` ("Both fire only on Claude-driven commits"); `doc-budget.yml:3-6` records the precedent
  ("commits made from the IDE or a terminal grew CLAUDE.md 20 KB past its cap unnoticed"). Five docs still call these
  Claude hooks "the pre-commit hook" (`.claude/rules/external-skill-ports.md:123`, `.claude/skills/finish-branch/SKILL.md:48`,
  `.claude/skills/deep-review/lenses/1-standards.md:35`, `docs/ai-includes/completion-workflow.md:99`,
  `docs/ai-includes/external-repo-adoption.md:33`), which reads as a git hook every committer hits.
- **Evidence (no git hook can run on this clone today)**: `.git/config` sets `core.hooksPath` to
  `c:\Users\mikew\source\repos\TAOM\.git\hooks`, the pre-relocation path (`c24f0dcd` moved the repo to `E:\repos\TAOM`),
  and `ls` of that path fails ("No such file or directory"). `.git/hooks/` holds only `*.sample`. So a hook installed
  the normal way would be silently ignored. This is local, untracked config: per `environment-failures.md` it is the
  user's to fix (`git config --unset core.hooksPath`, or point it at the committed dir below).
- **Evidence (the 10 subject misses were most likely made outside Claude)**: all ten
  (`c79a5852 b23a27ef ea0592eb 8b29f3b1 925db38d 83bdad85 2a9e8bc4 35007dbb d30dfd08 ef6afeb6`, 2026-09-15 to
  2026-09-23) share an editor-generated shape: imperative "Add/Implement/Refactor ..." subjects and unwrapped bullet
  bodies. Commits with a body line over 72 characters: 7 of the 10 misses versus 19 of the 85 conforming commits;
  average longest body line 148 versus 80 (awk over `git log --format=%b`). A search of every retained transcript under
  `~/.claude/projects/*TAOM*` for a Claude-issued `git commit` carrying any of the ten subjects found none (the only hits
  were this session's own greps and the session-start "Recent commits" block). **Caveat that limits the claim**: the
  subject gate emitted the top-level `permissionDecision` form from its creation (`8dabf4a6`, 2026-09-14) until the #647
  fix, and `harness-facts.md` records that the harness ignores that form (`git show 8dabf4a6:...` has 1 top-level and 0
  `hookSpecificOutput`; HEAD has 0 and 2). So in this window the hook could not block even a Claude commit, and "the hook
  would have blocked them" is not provable. Confidence that the ten were IDE commits: MED (style plus transcript
  absence, no positive proof).
- **Evidence (the cost is not only cosmetic)**: `c79a5852` "Add scripts for generating and validating animal animation
  clips" is a 208-file, 9,534-insertion sweep (`git show --stat`) that also carries the ADR-011 harness reset and the
  #647 repair (12 files under `.claude/hooks/`, 11 under `.claude/rules/`, 17 under `.claude/skills/`, 15 under
  `Main/Features/`). It is the commit that *adds* `docs/adrs/011-knowledge-delivery-tiers.md` and last rewrote
  `.claude/skills/skill-stocktake/SKILL.md` and `context-budget/scan.sh` (`git log --diff-filter=A`, `git log -1`).
  `git log --grep` for the hook fix, ADR-011 or #647 cannot find it by subject, and triage by
  `git log --grep 'v2.0.30 - '` (the rule's stated purpose, `check-commit-subject-version.sh:11-15`) misses all ten.
  Separately, 17 of the 95 subjects exceed the 72-character limit in AGENTS.md "Git and commits"; no gate checks length.
- **Evidence (the CI that exists is red and read by nobody)**: all 40 `Build & Test` runs listed (2026-09-07 to
  2026-09-15, `bannerlord-1.4.5`) concluded `failure`. In the last one, `Python Tool Tests` fails on
  `ModuleNotFoundError: No module named 'pytest'` and `'lxml'` plus `Path.read_text() got an unexpected keyword argument
  'newline'` (a Python 3.13 API on the runner's default Python); `Validate XML & XSLT` fails on "the documentation graph
  got structurally worse". At HEAD, 5 `tools/tests` modules `import pytest` (for example
  `tools/tests/test_analyze_melee_ladder.py:15`), 8 tools import `lxml`, and 4 call `read_text(..., newline="")`
  (`tools/generate_lord_template_equipment.py:98`, `tools/translate_with_claude.py:748`), while the `python-tests` job
  comment in `build.yml` still says "zero pytest imports ... needs no pip install", and the `hook-harness` job comment
  records the ratchet "has been failing since 2026-08-28". The C# job was `skipped` (no runner, no variable: seed F1).
- **Impact**: every rule the repo enforces at commit time is advisory for three of its four committers (IDE, terminal,
  Codex). The misses are measured: 10 of 95 subjects since the split, including one 208-file sweep, plus the doc-budget
  precedent. A permanently red CI trains everyone to ignore it, so enabling it on `bannerlord-1.5.x` as it stands would
  add noise, not a gate.
- **Fix sketch (smallest move, two parts)**:
  1. **Committed git hooks** in `.githooks/` (new, tracked) with `core.hooksPath=.githooks` set once per clone
     (documented in README setup; `session-start.sh` may *detect and print* a missing or stale `core.hooksPath`, never
     set it). `commit-msg`: subject regex and version against the staged `Main/_Module/SubModule.xml`, the AI-trailer
     regex, a 72-character subject check. It reads the final message file git passes, so it needs none of the
     shlex/heredoc reconstruction that makes `check-commit-subject-version.sh` 13.7 KB; once it exists, that Claude hook
     can go (a failing `commit-msg` surfaces in Claude's Bash output, and `block-no-verify.sh` stops the bypass).
     `pre-commit`: the staged-path triggers of today's hooks, calling the same tools (`validate_moduledata.py` with the
     22 codes, `pe_inspect.py`, the CHANGELOG-with-`.claude/` rule, the tracked-files rule). They fail open on their own
     faults (no Python: print "UNCHECKED" and exit 0), the same contract as the Claude hooks; a real violation exits 1.
  2. **One CI job that is green before anyone relies on it**: fix the three red causes (install `pytest lxml` and pin
     `python-version: "3.13"` in `python-tests`, or drop the pytest imports; re-baseline the doc-graph ratchet), then add
     `bannerlord-1.5.x` to `build.yml` `on.push/pull_request.branches` and a `commit-subjects` step that runs the same
     `commit-msg` checker over `git log --format=%B ${{ github.event.before }}..${{ github.sha }}` (after the fact, it
     catches a committer whose clone never set `core.hooksPath`). Server-side force-push protection is DX-L6-03.
- **Effort**: M (two hook scripts over one Python checker, three CI repairs, one trigger line, docs). **Risk**: LOW for
  the hooks (fail open, same tools); MED for CI on 1.5.x (it must be green first or it is ignored again).
- **Confidence**: HIGH on the gate inventory, CI state and hooksPath; MED on "IDE-made" for the ten.
- **Delta**: introduced (the subject gate, the 1.5.x branch and the red runs all post-date `141b749`; triage-B L533/L571
  hold the older "CI dark" half and seed F1 the C# half; this adds committer coverage and the red-CI evidence).
  **P1**: no. **Plan candidate**: yes: self-contained and testable (feed the checker the ten miss messages and the 85
  passes), and it is the precondition for trusting CI at all.

### [DX-L6-02] Put a bash-only prefilter in front of the Bash hooks: every non-git command pays a Python probe and a Python JSON parse in 12 processes

- **Measurement**: read all 13 Bash-path scripts at `b2e387db`; decomposed one hook's cost with 5-run averages in Git
  Bash on this machine (`date +%s%N` around each command, `TAOM_PYBIN=C:/Python314/python.exe` as `settings.json` sets
  it): bare `bash -c "exit 0"` **29 ms**; `bash` plus `source .claude/hooks/_pybin.sh` **100 ms** (104 ms without the
  pin); one `python -c` JSON parse of the payload **63 ms**; the `_pybin` probe alone (`python -S -E -c`) **46 ms**.
  Then the harness shape (all matching hooks in parallel), same non-git payload `{"tool_input":{"command":"ls docs"}}`:
  the 10 PreToolUse(Bash) gates launched together **340 to 408 ms wall** (5 runs, mean 383); the 2 PostToolUse(Bash)
  hooks together **235 to 297 ms** (mean 267); a bash-only prefilter script (`INPUT=$(cat)`, one `[[ =~ ]]`, exit)
  **62 to 72 ms** alone and **205 to 241 ms** when 10 copies start at once. `suggest-compact.sh` was not run (it writes a
  counter); `mark-verification-run.sh` was, and did not touch its marker (mtime unchanged at 18:40).
- **Evidence (why 220 to 360 ms each)**: every one of the 12 non-universal Bash hooks starts with
  `source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"`, and `_pybin.sh` ends with `PYBIN=$(taom_resolve_python || true)`
  at file scope, which validates the pin and then **executes Python** to prove it is live
  (`taom_pybin_probe`: `timeout -k 0.2 0.8 "$1" -S -E -c 'import sys; sys.stdout.write("taompy")'`). The hook then
  pipes the whole payload to a **second** Python process to read one field (`block-no-verify.sh`, `validate-push.sh`,
  `check-changelog-changed.sh`, `check-claude-files-tracked.sh`, `check-commit-subject-version.sh`,
  `block-dangerous-git.sh`, `block-broad-git-add.sh`, `check-moduledata-validation.sh`, `check-native-dll-crt.sh`,
  `check-doc-config-drift.sh`, `notify-test-results.sh`, `mark-verification-run.sh`; `jq` is preferred where coded
  but is not on PATH here, `block-no-verify.sh` header). Only after both Python spawns does each script test whether
  the command is a git command at all. No git call runs on the non-git path (each `git diff --cached` sits after the
  `git commit` case test), so the 220 to 360 ms is: bash spawn about 30, probe about 70, parse about 60, plus pipes and
  the `validate-push.sh` token loop. `_pybin.sh`'s own note already measured this ("~77ms per hook, ~850ms of CPU
  across the ten PreToolUse hooks") and chose the pin, which saves the search but not the probe.
- **Evidence (the early exits are all git- or dotnet-shaped)**: all 10 PreToolUse gates exit allow when the command
  holds no `git` token (`validate-push.sh` requires a `git` token before `push`; the dangerous/broad-add pair inspect
  only segments starting `git`; the other six need `git commit`); `notify-test-results.sh` needs `dotnet test`;
  `mark-verification-run.sh` needs `dotnet build|test` or `build.ps1`; `suggest-compact.sh` runs its counter in pure
  bash and loads Python only to detect a commit boundary. So a raw-payload test "does the text `git` (or `dotnet`,
  `build.ps1`) occur anywhere" is a strict superset of every hook's own trigger: when it is false, today's hooks already
  allow, so skipping them changes no decision.
- **Scale**: `grep -o '"name":"Bash"'` over the 20 newest main-session transcripts in
  `~/.claude/projects/e--repos-TAOM/` gives up to 2,173 Bash calls in one session (`846fe3ce`), 836, 669, 358 in others;
  commands whose first word is `git` (or `cd ... && git`) are 5 to 12% of them. So about 90% of Bash calls pay the full
  cost for nothing.
- **Design** (respects `harness-facts.md` fail-open and parallel/kill semantics and `hook-authoring.md` "timeout
  measured against the slow path"):
  1. **Step 1, no restructuring (a few lines per script)**: move the prefilter above the `source` line in each of the
     12 scripts: `INPUT=$(cat); [[ "$INPUT" =~ (^|[^[:alnum:]_.-])git([^[:alnum:]_-]|$) ]] || { echo '{}'; exit 0; }`
     (the dotnet form for the two PostToolUse hooks). Matching the raw JSON, description included, can only
     over-include, which is the safe direction. The degraded-parser warning is untouched on the git path, and nothing
     that could be a gate is skipped. Measured effect: PreToolUse wall 383 to about 225 ms; CPU for the 10 gates from
     2,834 ms (baseline serial sum over 11) to about 10 x 65 = 650 ms.
  2. **Step 2, one dispatcher for the fast gates**: replace the 7 sub-second gates (`block-no-verify`, `validate-push`,
     `block-dangerous-git`, `block-broad-git-add`, `check-changelog-changed`, `check-claude-files-tracked`,
     `check-commit-subject-version`) with one registration `pretool-bash-dispatch.sh` (timeout 15 s): prefilter in bash;
     on a git payload source `_pybin.sh` once, parse `tool_input.command` once, export `COMMAND`, then run each gate as
     a function in its own subshell under `timeout -k 1 <its budget>`; any rc 124 or unparseable output becomes an
     `ask` naming the gate (never a pass), the first `deny` wins, otherwise the `ask` reasons are joined, otherwise `{}`.
     Keep `check-moduledata-validation.sh` (45 s inner bound), `check-doc-config-drift.sh` (20 s) and
     `check-native-dll-crt.sh` as their own registrations with the Step 1 prefilter, so no slow path shares a budget.
     Merge the two PostToolUse(Bash) hooks into one the same way. `tools/test_hooks.sh` 4b (answers inside 80% of the
     registration) and 5c (nested output form) then apply to the dispatcher as one gate, plus a new check that each
     function's inner bound sums below the dispatcher's timeout.
  3. The documented `if:` handler field (`"Bash(git commit*)"`) would skip the spawn entirely, but `harness-facts.md`
     defers it ("a mis-scoped `if:` silently disables a gate, so it needs a per-gate proof"); the prefilter needs no
     such proof, so it goes first.
- **Estimated saving per non-git Bash call** (from the numbers above): PreToolUse wall 383 ms to about 64 ms (one
  dispatcher plus three prefiltered slow gates starting together is about 4 bash spawns, between the 1-copy 64 ms and
  the 10-copy 225 ms; call it 100 to 130 ms), PostToolUse 267 to about 64 ms. So about **450 to 500 ms of wall per call**,
  about 3 s of CPU, and 9 fewer processes. For the 2,173-call session that is on the order of 16 to 18 minutes of wall
  (hooks block the tool call before and after it runs). Git commands keep today's cost.
- **Impact**: pure latency and CPU; no gate decision changes on the non-git path by construction.
- **Effort**: S for Step 1, M for Step 2 (dispatcher, the test_hooks.sh check, a live proof per `hook-authoring.md`
  "Prove a gate live"). **Risk**: LOW for Step 1 (superset trigger); MED for Step 2 (a dispatcher bug takes down seven
  gates at once, so it must fail open per gate and ship with the live proof).
- **Confidence**: HIGH on the mechanism and the single-machine timings; the harness's own spawn overhead on top of these
  numbers was not measured.
- **Delta**: introduced (`_pybin.sh` and most gates post-date `141b749`). **P1**: no. **Plan candidate**: yes for
  Step 1 (mechanical, a clean before/after timing), Step 2 as a follow-up.

### [DX-L6-03] Protect `bannerlord-1.5.x` from force pushes: the only guard names the old trunk, and GitHub protects neither branch

- **Measurement**: `git show b2e387db:.claude/hooks/validate-push.sh`; `git grep -n "1\.5\.x" b2e387db -- .claude/hooks`
  (0 hits); `gh api repos/haterade22/TAOM/branches/bannerlord-1.5.x` and `.../bannerlord-1.4.5` (`"protected": false`);
  `gh api repos/haterade22/TAOM/rulesets` (0).
- **Evidence**: `validate-push.sh:115` `master|main|bannerlord-1.4.5) return 0 ;;` is the whole protected list; the
  comment above it (`:110-112`) records that `bannerlord-1.4.5` was itself missing until 2026-08-20 "so a force push to
  the branch everyone works on passed unchallenged". The work moved to `bannerlord-1.5.x` (the release tags `v2.0.29` `a12fec9b` and
  `v2.0.30` `96f17fec` are ancestors of `bannerlord-1.5.x` and not of `bannerlord-1.4.5`, `git merge-base --is-ancestor`)
  and the list did not follow. Server side there is
  nothing: no branch protection and no ruleset on the public repo.
- **Impact**: a Claude `git push --force origin bannerlord-1.5.x` (or `+bannerlord-1.5.x`, or `HEAD` while on it) passes
  the hook with at most no message; a force push from any other client is unguarded anywhere. With about 80 uncommitted
  paths of another session sitting on this branch and commits "COMMITTED, NOT pushed" in memory, a rewrite of the
  remote branch is the costliest git accident available here.
- **Fix sketch**: add `bannerlord-1.5.x` (better, `bannerlord-*`) to `is_protected`, and enable a GitHub ruleset on
  `bannerlord-*` that blocks force pushes and deletion (no required checks until DX-L6-01 makes CI green). The ruleset is
  a repo setting the owner applies; the hook line is a one-line repo change.
- **Effort**: S. **Risk**: LOW (a deliberate history rewrite then needs the ruleset lifted, which is the point).
- **Confidence**: HIGH. **Delta**: introduced (the branch post-dates `141b749`). **P1**: no (not crash, save or security
  in the sense of the rubric, but it guards against losing pushed history). **Plan candidate**: yes, fold into the
  DX-L6-01 plan as its first, cheapest step.

### [DX-L6-04] Make a shipped build traceable: stamp dirty trees, put the build stamp in the crash bundle, and package only a DLL built at the release tag

- **Measurement**: read at `b2e387db`: `Directory.Build.props:12-24`, `Main/Core/Diagnostics/BuildStampReport.cs:104-147`,
  `Main/Features/CrashReport/Collectors/IdentityCollector.cs` (whole file), `AssemblyListCollector.cs:23-34`,
  `Rendering/PlainTextCrashReportRenderer.cs:67`, `SaveLoadDiagnostics/Hooks/MBSaveLoad_GetSaveMetaData_Patch.cs:18-38`,
  `docs/reference/release-process.md` (whole "Cutting a release"), `.claude/skills/release/SKILL.md:22-100`,
  `tools/package_release.py:1-37,134-135,299-380`, `build.ps1` (`grep -n -i "git|dirty|stamp|tag|version"`: no hit).
  `git status --porcelain -- Main Dependencies Directory.Build.props` right now: 21 paths, 43 to 46 ms (3 runs).
- **Evidence (a dirty build looks clean)**: `Directory.Build.props:23-24` sets `InformationalVersion` to
  `build.<UTC stamp>`; the SDK appends `+<HEAD sha>` (seed F7 as corrected, live log line 2). Nothing reads the working
  tree, so with the 21 uncommitted compiled-input paths present now, a build reports HEAD's SHA and cannot be told from a
  clean build of HEAD. The seed's live log line (`...+c79a585218ad...`) is exactly that: it was logged while this tree
  held another session's uncommitted C# on top of `c79a5852`.
- **Evidence (the crash bundle does not carry the stamp or the SHA)**: `IdentityCollector.Collect` records
  `TaomVersion` (the `SubModule.xml` label), `TaomDllSha1` (a hash of the loaded DLL file), the Bannerlord versions and
  the language; `AssemblyListCollector.cs:28` records `AssemblyName.Version`, which `Directory.Build.props` freezes
  (`2.0.0.0` in the seed's log line). The only copy of the stamp in a bundle is the `[BuildStamp]` startup line, and the
  bundle keeps the last 500 lines of `taom_debug.log` (lane 3 CORRECTNESS-02 read `LogTailCollector.cs:13,25-41`), so any
  session long enough to crash has usually rotated line 2 out. `TaomDllSha1` resolves to a commit only if someone
  recorded each shipped DLL's hash; no such registry exists (`git grep -n -i "dll sha1|DllSha1" -- docs/releases` finds
  nothing). Saves are better off than crash bundles: `MBSaveLoad_GetSaveMetaData_Patch.cs:18-38` writes the full
  `InformationalVersion` into every save's metadata as `TAOM_Build`.
- **Evidence (a player release can be built from an uncommitted tree, and from the wrong commit)**:
  1. `tools/package_release.py:1-12` turns "a TAOM dev install" (`--source "<game>/Modules"`) into the release; it has no
     git awareness at all (no `git`, tag or dirty check in the file). What ships is whatever `build.ps1` last deployed,
     from whichever worktree or branch built last.
  2. `/release` Phase 2 builds and tests (`./build.ps1 -RunTests`) **before** Phase 3 bumps the version and Phase 6
     commits, so the DLL that exists when the tag is cut was built from the release commit's *parent* plus the
     uncommitted bump, and carries the parent's SHA. Neither the skill nor `release-process.md` says to rebuild after
     the tag, and neither mentions `package_release.py` at all (`grep -n package` on the skill: 0).
  3. `release-process.md` "Cutting a release" step 1 allows the release "when another session's edits are present,
     every path staged explicitly and theirs left out". The staging keeps them out of the *commit*; `build.ps1` still
     compiles and deploys them, so the shipped DLL can contain code that no commit, and therefore no tag, holds. The skill
     (`SKILL.md:24`) says the opposite ("`git status --porcelain` is **empty**"), so the two sources disagree on the
     precondition that matters most here.
- **Impact**: the stated purpose of the tag contract (`release-process.md`: "`git show v2.0.18:<path>` reconstructs
  exactly what shipped") does not hold for the binary: a player's crash bundle names a label shared by every commit since
  the last bump (the reason the subject-label rule exists) plus a DLL hash nothing maps back. Triage of a player CTD can
  then chase code that never shipped, or miss code that did.
- **Design**:
  1. **Dirty stamp (MSBuild, no-git fallback)**, in `Directory.Build.props`, one target:
     `<Target Name="TaomStampWorkingTree" AfterTargets="InitializeSourceControlInformation" BeforeTargets="AddSourceRevisionToInformationalVersion" Condition="'$(TaomSkipGitStamp)' != 'true'">`
     running `<Exec Command="git status --porcelain -- Main Dependencies Directory.Build.props" WorkingDirectory="$(MSBuildThisFileDirectory)" ConsoleToMSBuild="true" IgnoreExitCode="true" StandardOutputImportance="low">`
     capturing `ConsoleOutput` and `ExitCode`; then append `.dirty` to `SourceRevisionId` when the exit code is 0 and the
     output is non-empty, and set it to `nogit` when the exit code is non-zero or `SourceRevisionId` is empty (a source
     zip, git missing). Result: `build.20260923-184249Z+c79a5852....dirty`. Untracked files count (the SDK compiles every
     `*.cs` under the project), docs do not (scoped to compiled and deployed inputs). Cost about 45 ms per build. The
     exact SDK target names must be confirmed with `dotnet build -v:diag` before relying on the ordering (UNVERIFIED
     here; no build was run by rule).
  2. **Crash bundle**: add `TaomBuild` (the TAOM assembly's `AssemblyInformationalVersionAttribute`, the same read as
     `BuildStampReport.cs:189-202`) to `IdentitySnapshot` and print it on the `TAOM:` line
     (`PlainTextCrashReportRenderer.cs:67`) and in the HTML renderer. One field, no new collector.
  3. **Tag-only release builds**: `/release` gains a step after Phase 7: rebuild at the tag in a clean worktree
     (`git worktree add <tmp> vX.Y.Z`, `dotnet build ... -c Release -p:TaomBuildStamp=<tag date>`), deploy that, then
     package. `package_release.py` gains `--require-build <tag>`: read `ProductVersion` from the PE version resource of
     `Modules/TAOM/bin/Win64_Shipping_Client/TAOM.dll` (it is the `InformationalVersion`), and refuse unless it ends with
     `+<git rev-parse <tag>^{commit}>` and carries no `.dirty` or `nogit`. Once CI compiles C# (seed F1), a
     `on: push: tags: ['v*']` job builds the two DLLs at the tag and uploads them with a SHA256 manifest, and packaging
     takes the DLLs from that artifact; the Armory and TAOM_Map stay local (unversioned by decision), so CI cannot build
     the whole zip.
  4. Reconcile `release-process.md` step 1 with the skill: clean tree, or a clean worktree at the release commit.
- **Effort**: S for 1 and 2 (plus unit tests: `TryParseStamp` already tolerates a suffix after the stamp,
  `BuildStampReport.cs:116-118`); M for 3. **Risk**: LOW: metadata only; `TryParseStamp` slices the stamp by fixed width,
  so a longer tail parses.
- **Confidence**: HIGH on what the bundle and the release flow do; MED on the SDK target names in the design.
- **Delta**: introduced (release process, tags and the crash bundle identity all post-date `141b749`; this extends seed
  F7). **P1**: no. **Plan candidate**: yes: 1 and 2 are a small, testable change with a clear before/after in the log and
  a crash bundle.

### [DEPS-L6-05] Repo weight: 80% of the 3.27 GiB pack is superseded binaries; put the atlas and art classes in LFS going forward, give code worktrees a sparse checkout, and do not rewrite history

- **Measurement**: `git rev-list --objects --all | git cat-file --batch-check='%(objecttype) %(objectname) %(objectsize:disk) %(rest)'`
  (24,914 blobs, 3.26 GiB on disk, matching `git count-objects -vH` 3.27 GiB), split by whether the blob id is in the
  `b2e387db` tree (`git ls-tree -r`) and by extension (`l6_weight.py` in the scratchpad). Growth: new blob ids from
  `git log --all --since=2026-06-23 --raw --no-abbrev --no-renames` (A/M only), sized with `cat-file`. Worktrees:
  `git worktree list` and `du -sm` of the orchestrator's detached worktree. Pattern sweep for generated or personal files:
  `git ls-files | grep -i -E "\.(log|dmp|mdmp|bak|orig|rej|tmp|cache|pkl|pyc|swp|old)$|(^|/)(crashz|logs?|backups?|bak|tmp|temp|cache|__pycache__|obj)(/|$)|settings\.local|\.user$|\.suo$|Thumbs\.db"`.
  Quotas: GitHub docs "Git LFS billing" (fetched 2026-09-23): Free and Pro include 10 GiB storage and 10 GiB bandwidth
  per month, bandwidth resets each cycle, a $0 budget blocks LFS for the rest of the month once exceeded, and an Actions
  download of an LFS file counts against the owner's bandwidth; the per-GiB prices ($0.07 per GiB-month storage,
  $0.0875 per GiB downloaded) come from the search snippet of the same docs page, not a page I read in full.
- **Evidence (where the pack weight is)**: blobs in the HEAD tree 739 MiB on disk; blobs **not** in HEAD **2,603 MiB**
  (80%): superseded PNG versions 1,645 MiB, deleted `.ogg` 541 MiB, deleted `Main/_Module/RuntimeDataCache` `.rdc`
  366 MiB on disk (13.7 GB raw; `.gitignore` records "5.4 GB of .rdc got committed by a game->repo sync (untracked +
  removed 2026-08-05)"). By directory, the superseded PNGs are `Main/_Module/AssetSources/GauntletUI` 1,065 MiB and
  `GUI/SpriteData/FactionMap` 421 MiB (+ `FactionMap_backup` 69 MiB, gone from HEAD). `AssetSources/GauntletUI` is
  generator output: `docs/features/gui-sprite-system.md:115` "the bin-packed atlas PNG; this dir is **emptied then
  rewritten** every run", 52 files and 242 MiB at HEAD, 28 commits touching it; each sprite bake re-commits 16 to
  30 MB sheets (`ui_taom_career_system_1.png` 30.4 MB, `ui_taom_1.png` 21.6 MB).
- **Evidence (the PSDs)**: 34 `.psd`, 309 MiB at HEAD, 33 in `Main/_Module/AssetSources/BannerIcons/` and 1 in
  `AssetSources/main_map_textures/`. They are the sources of the banner textures: `docs/modding/banners-and-heraldry.md:156`
  ("place the source under `Main/_Module/AssetSources/BannerIcons/` as `taom_banners_<faction>_alpha_NN.psd`") and
  `docs/reference/banner-icon-generation.md:67` (importing it produces only the `_tex.tpac`). Nothing ships them:
  `tools/package_release.py:134-135` excludes `AssetSources` ("editor-only, never loaded at runtime"), and
  `docs/modding/module-taom.md:46` records "86 raw art files (PSD and PNG), 551 MB ... the dev build still deploys it
  (`Main/TAOM.csproj:13`)". No other home is documented: `git grep -i -E "azure|blob storage|dropbox|google drive|git lfs"`
  over docs, tools, `.claude`, README and AGENTS finds only engine crash-uploader DLL names and OneDrive save paths, so
  git is the only copy of record. They churn little (9 new versions, 36 MiB in 90 days).
- **Evidence (F5 extended)**: the pattern sweep finds nothing beyond seed F5's three (`.claude/settings.local.json`,
  `_taom_loc.pkl`, `crashz/`) except `.claude/tmp/freeze/.gitignore`, a deliberate placeholder. `git check-ignore -v`
  matches none of the F5 three, so they would be re-added after a delete. How they got in:
  `git log --diff-filter=A` gives `crashz/` in `d9817f89` "Refactor code structure for improved readability and
  maintainability" (8 files, 6,862 insertions), `_taom_loc.pkl` in `b930fd8a` "feat: Add tests and documentation for
  lord identity reconciliation" (42 files), `settings.local.json` in `78892259` "Updpdate": all unlabeled sweep commits of
  the DX-L6-01 shape, so a `pre-commit` size and path guard there (new file over 5 MB outside an allowlist, or any
  `crashz/`, `*.pkl`, `settings.local.json`) would have stopped all three. `docs/reference/troop-rosters.html` (4.5 MB,
  generated) is tracked on purpose (`tools/generate_troop_roster_page.py:19`), so it is not a finding.
- **Evidence (growth and worktrees)**: new binary blob versions in the last 90 days: PNG 694 (724 MiB raw), tpac 111
  (34 MiB), PSD 9 (36 MiB), pkl 1 (35 MiB), rdc 78 (3,392 MiB, before the 2026-08-05 ignore); in the last 30 days only 5
  (36 MiB). One worktree (`wt-opus-head`, `b2e387db`) is **1,235 MB** on disk (`du -sm`), of which `Main` 996 MB; there
  are **9** worktrees on this machine (`git worktree list`: the main tree, 5 more under `E:\repos`, 2 in session
  scratchpads, 1 detached `wt-mumakil`), so about 11 GB of checkouts. `Main/_Module/AssetSources` alone is 550 MiB of the
  1,154 MiB HEAD tree, and no test reads it (`git grep -l AssetSources -- TAOM.Tests`: 0 files; the one test that touches
  `ModuleSounds` checks file existence, `ShippedSignatureStrikesConfigTests.cs:225-226`).
- **Options, priced**:
  1. **LFS going forward only** for `AssetSources/**`, `GUI/SpriteData/**/*.png`, `*.psd`, `*.wav`, `*.mp3`, `*.ogg`,
     `*.tpac` (the sibling asset repo already tracks `*.tpac` through LFS, `docs/reference/armory-catalogue/README.md:72`).
     First storage is the HEAD binaries, about 1.0 GiB (png 501 + psd 324 + wav 112 + mp3 62 + tpac 36 MB from
     `baseline.md`); growth at the 90-day PNG/tpac/PSD rate is about 0.26 GiB a month, so the free 10 GiB lasts roughly
     three years, then about $0.07 per GiB-month. Bandwidth: every fresh clone pulls about 1 GiB, so the 10 GiB covers
     about ten clones a month; CI must keep `actions/checkout`'s default `lfs: false` (no workflow needs the art), or ten
     runs exhaust it. The existing 3.27 GiB pack does **not** shrink, and worktrees still materialise the files unless
     created with `GIT_LFS_SKIP_SMUDGE=1`. It adds a client dependency (`git lfs install`) for every committer and for
     the Modding Kit round trip, which reads the files on disk (smudged, so unaffected).
  2. **History rewrite** (`git lfs migrate import --everything` or `git filter-repo --invert-paths` on the RDC,
     `FactionMap_backup` and deleted OGGs): would cut the pack by up to about 2.6 GiB, but rewrites all 1,986 commits:
     the 26 tags (the crash-report lookup contract in `release-process.md` says "Never move a pushed tag"), at least 833
     backticked SHAs cited in docs, CHANGELOG and `.claude` (`git grep -o -E '`[0-9a-f]{7,10}`'`), the SHA inside every
     shipped build's `InformationalVersion` and every save's `TAOM_Build`, the 49 local and remote branches, all 9
     worktrees and any fork or mirror. `migrate import --everything` would also move the 13.7 GB raw RDC into LFS, far
     over quota. **Not worth doing**: the pack costs one clone download; the rewrite costs every provenance link the
     release process was built on.
  3. **Sparse checkout for code worktrees** (no LFS, no rewrite): `git sparse-checkout set --no-cone '/*' '!/Main/_Module/AssetSources/'`
     right after `git worktree add` in the worktree recipes (`docs/ai-includes/agent-teams.md`, the `/improve` and
     `/deep-review` worktree steps). Saves about 550 MiB of the 1,235 MB per worktree (45%) today, with no test impact by
     the grep above. Builds with `-p:DisableModuleCopy=true` deploy nothing, so the missing art cannot reach the game.
- **Recommendation**: 3 now (S), plus the F5 cleanup and ignore lines and the size guard folded into DX-L6-01's
  `pre-commit`; 1 only when the owner accepts `git lfs` on every machine that commits art (the two dev machines, plus
  the four other authors in `git shortlog -sne --all`: Arystar 14, James Parkinson 11, theb0yys 2, Sternab 1), starting with `AssetSources/GauntletUI`, which carries 65% of the superseded weight and is regenerable.
- **Effort**: S (sparse recipe, F5 removal), M (LFS: `.gitattributes`, one conversion commit, docs, CI `lfs: false`).
  **Risk**: LOW for 3; MED for 1 (a committer without `git lfs` commits pointers or full blobs inconsistently).
- **Confidence**: HIGH on the measurements; MED on the prices (snippet, not the full page) and on the growth forecast
  (90-day rate, dominated by the sprite work and texture downsizing of that period).
- **Delta**: pre-existing weight, introduced artifacts (F5 files, the RDC episode and the atlas churn all post-date
  `141b749` except `settings.local.json`). **P1**: no. **Plan candidate**: yes for option 3 plus F5 (small, reversible);
  LFS as a decision item, not a plan.

### [DX-L6-06] The four Stop reminders (verification, deep review, version tag, CHANGELOG) print to a channel Claude never sees, then mute themselves

- **Measurement**: `git show b2e387db:.claude/hooks/<h>.sh | grep -n -E "exit [0-9]|>&2|decision"` for the four Stop
  registrations in `.claude/settings.json`; the harness contract as the repo itself records it.
- **Evidence**: each prints its reminder with `echo "REMINDER: ..." >&2` and ends `exit 0`:
  `check-verification-evidence.sh:46,56`, `check-deep-review.sh:33,36`, `check-version-tagged.sh:49,54`,
  `check-changelog-updated.sh:35,44`. The repo's own docs say that output is invisible: `harness-facts.md` "Visibility"
  ("Stderr from a hook that exits 0 never reaches Claude"; DOC 2026-09-23 plus an EMPIRICAL note that `suggest-compact.sh`
  printed about 18 times and none arrived) and `docs/reference/hooks-catalog.md:53-56` ("Stdout from Stop ... and stderr
  from a hook that exits 0, go to the debug log only"). Yet the same catalog's rows (`:24,25,33,44`) describe them as
  reminders, `block-no-verify.sh` and `hooks-catalog.md:22` name `check-verification-evidence.sh` as where build
  verification "stays" after the build gate was dropped, and `release-process.md` "Cutting a release" relies on
  `check-version-tagged.sh` for the step "that gets skipped". Each also writes a mute marker when it fires
  (`check-version-tagged.sh` header: "mutes itself after one reminder per streak"), so the one reminder per streak is
  spent on the debug log. `notify-test-results.sh` (PostToolUse, `>&2`, exit 0) is the same class but harmless (the
  test output is already in the tool result).
- **Impact**: the only machine backstops for "no done without a build", "tag the release" and "review before close" are
  silent while the docs present them as active. This is the "no output reads as nothing to report" failure
  `hook-authoring.md` warns about, at the event level. It also bears on DX-L6-01: the verification and tag reminders were
  the substitute for commit-time gates.
- **Fix sketch**: switch each to Stop decision control: print `{"decision":"block","reason":"<reminder>"}` on stdout
  (the top-level `decision` form `harness-facts.md` lists for non-PreToolUse events), which keeps Claude working and hands
  it the reason; return `{}` when the payload's `stop_hook_active` is true, so a reminder can never loop; write the mute
  marker only after emitting. Then prove one live per `hook-authoring.md` "Prove a gate live" and fix the catalog rows.
  Fold `check-changelog-updated.sh` into DX-L6-07 (it goes away there).
- **Effort**: S. **Risk**: LOW to MED (a reminder that blocks Stop every turn would be noisy; the per-streak markers and
  `stop_hook_active` bound it).
- **Confidence**: HIGH that the scripts use the invisible channel (read); MED that nothing surfaces them at all, since I
  did not trigger a live Stop in this read-only run (the repo's own DOC and EMPIRICAL entries are the evidence).
- **Delta**: introduced (all four Stop hooks and the visibility fact post-date `141b749`; triage-B L563 covers only the
  deep-review hook's "cannot prevent a commit" half). **P1**: no. **Plan candidate**: yes: four small edits with a
  test_hooks.sh check ("a Stop hook that emits must use decision control") and one live proof.

### [DOCS-L6-07] Stop hand-editing a 1.76 MB shared CHANGELOG: generate it at `/release` from the commit bodies that already carry the same text

- **Measurement**: incident search `git log --all -i --grep=changelog` filtered for conflict, lost, restore, drop,
  stash; merge conflicts on the file: for each of the 29 merges in `git log --all --merges`, whether
  `git show --cc -- CHANGELOG.md` has a combined hunk (`^@@@`); overlap with commits: `### ` headings in
  `git show b2e387db:CHANGELOG.md` under the dated sections from 2026-09-14 on, against the 95 commits since the split;
  instruction weight: `git grep -c -i changelog` over AGENTS.md, CLAUDE.md, `.claude/rules`, `docs/ai-includes`, skills.
- **Evidence (collisions and loss)**: 7 of 29 merges carry a resolved CHANGELOG hunk (`9c532b5e`, `07a54efe`,
  `828bf941`, `38246fea`, `2c9aeeee`, `6ebbbd48`, `f89ef618`; five of them on 2026-08-08). Recorded losses and repairs:
  `dc2d13a5` ("restore the #583 Codex follow-up hunks dropped by 94552135": a commit wrote "stale reconstructions from
  another session" of CHANGELOG.md and undid another session's entries, and "also dropped the ranged-ladders session's
  ... CHANGELOG entries (#582, #588 ...)"); `a035a2d3` (a CRLF rewrite turned a 60-line entry into a 22,907-line diff);
  `41d4afbe` (two #434 entries swept into an unrelated pushed commit). `docs/ai-includes/git-and-commits.md` carries
  three CHANGELOG-specific rules born of incidents: `:56` (an entry appended into live `<<<<<<<` markers, 2026-08-08),
  `:60` ("Never write a reconstructed shared file to disk ... 2026-08-28: two entries lost"), `:61` (a nine-line paragraph
  "vanished to a concurrent operation before it could be staged"). Baseline: 545 of 809 commits since 2026-07-01 touch
  the file, 1,759,092 B.
- **Evidence (it duplicates the commit log)**: since 2026-09-14 the file has 97 `### ` entries against 95 commits, and 37
  of those headings are literally a conventional, version-labelled subject (for example the top entry
  "fix(config): v2.0.30 - Review 130 follow-ups: list entries, formation names" versus commit `b2e387db` "...: list
  entries, formations", whose body carries the same bullets). The version label the subject gate enforces is exactly the
  key a generator needs: `git log --grep 'v2.0.30 - '` already selects a release's commits (`release-process.md` step 6).
- **Options compared**:

  | | towncrier-style fragments | git-cliff-style generation from commits |
  |---|---|---|
  | Source of an entry | one new file per change, `changelog.d/<issue-or-slug>.<type>.md` | the commit subject and body |
  | Collision | none (new files never conflict) | none (nothing shared is written) |
  | Extra writing per change | a fragment in addition to the commit body | none; the body already is the entry |
  | Editable after push | yes, until `/release` compiles | no (fix in the release note) |
  | Needs | towncrier or a 60-line stdlib compiler run by `/release` | git-cliff (Rust binary, also on PyPI) or a stdlib script over `git log <prev-tag>..<tag>` |
  | Weak spot | a fragment dir of about 100 files per release | commits that miss the format (10 of 95 today) produce poor entries until DX-L6-01 lands |
  | Fits TAOM | if the owner wants player-facing prose distinct from commit bodies | best: Claude's bodies are already written as entries, and the label is gated |

  Recommendation: generate from commits (stdlib script or git-cliff with a template keyed on the `vX.Y.Z - ` label), run by
  `/release` Phase 5 into `docs/releases/vX.Y.Z.md` and a regenerated `CHANGELOG.md` section; freeze today's file as
  `docs/changelog-archive/CHANGELOG-2026-H2.md` at the cut-over, as the H1 archive was. Fragments are the fallback if
  bodies are not rich enough for players.
- **What the rules would change to**: AGENTS.md "Documentation duty" (`AGENTS.md:80` "CHANGELOG.md is updated every
  session") becomes "the commit body is the changelog entry; write it for a reader of the release note; `/release`
  generates CHANGELOG.md". AGENTS.md:34 and :40 drop the word CHANGELOG (commit bodies already covered). CLAUDE.md:56
  `/ship` row loses "CHANGELOG". Then deletable: `check-changelog-changed.sh` (5,177 B, a PreToolUse gate) and
  `check-changelog-updated.sh` (1,797 B, silent anyway, DX-L6-06) with their settings rows and catalog rows;
  `git-and-commits.md:56,60,61` shrink to one general shared-file rule; the CHANGELOG steps in
  `docs/ai-includes/completion-workflow.md`, `.claude/rules/external-skill-ports.md:123,132` and the 17 skills that
  mention CHANGELOG (`git grep -l -i changelog -- '.claude/skills/*/SKILL.md'`), plus `hook-authoring.md`'s CHANGELOG
  examples (3 mentions).
- **Impact**: removes the single most contended file in the repo (touched by two thirds of all commits), the recorded
  data-loss class around it, two hooks and three git-safety rules, and one duplicate write per commit.
- **Effort**: M (generator, `/release` phase, one-time archive, rule edits across about 20 files). **Risk**: MED: the
  owner must accept that between releases "what changed" is `git log`, not a file; IDE commits need DX-L6-01 first.
- **Confidence**: HIGH on the incident and overlap counts; the choice between the two is a preference call for the owner.
- **Delta**: introduced (the volume and incidents are July to September). **P1**: no. **Plan candidate**: yes, after
  DX-L6-01 (the generator depends on well-formed subjects).

### [DOCS-L6-08] The front door is wrong on version, branch and the test command: README (beyond seed F8) and one agent command table

- **Measurement**: `git show b2e387db:README.md` read in full (200 lines); facts checked against `.claude/pinned-game-version.txt`
  (`v1.5.3`), `gh api repos/haterade22/TAOM --jq .default_branch` (`bannerlord-1.4.5`), `git rev-list --count a4c6d7c3..<branch>`,
  `git show b2e387db:setup-dev-env.ps1`, the career XML parsed with `xml.etree` (`Career` elements, comments excluded),
  and `git grep -n -E "dotnet (test|build)"` over entry docs, filtered for lines lacking `-p:DisableModuleCopy=true -p:ModuleId=`.
  Why the linter misses these: `tools/lint_docs.py:40` scans `DOCS_DIR = REPO_ROOT / "docs"` (root `README.md` is outside
  it), its stale-version patterns (`:92-98`) list 1.3.x and older 1.4.x forms, and the pin check (`:634-667`) reads only
  the `Target: Bannerlord` line in AGENTS.md and CLAUDE.md.
- **Evidence (actively wrong, each contradicted by the tree)**:
  - **Game version, three answers in one file**: `README.md:3` "for **Mount & Blade II: Bannerlord v1.4.8**", `:37`
    prerequisite "Bannerlord **v1.4.8** installed", `:175` (the player install section) "Bannerlord **v1.5.2** is
    required". The pin and `Main/_Module/SubModule.xml:32` (Native `v1.5.3.*`, triage-check item 13) say v1.5.3. A player
    following `:175` installs the wrong beta.
  - **Active branch**: `README.md:19` "The active development branch (and the GitHub default) is **`bannerlord-1.4.5`**".
    Since the split: 95 commits on `bannerlord-1.5.x`, 4 on `bannerlord-1.4.5` (last `c5b84fb4`, 2026-09-15); `v2.0.29`
    and `v2.0.30` are tagged on the 1.5.x line only. The "GitHub default" half is true, which is its own problem: a visitor,
    a PR and the only CI trigger (DX-L6-01) all land on the line that no longer moves. (Which line players get is a
    release-channel decision; the README claim about where development happens is simply false.)
  - **The test command deploys**: `README.md:58` `dotnet test TAOM.Tests     # tests only`. AGENTS.md "Commands" gives the
    non-deploying form (`-p:DisableModuleCopy=true -p:ModuleId=`) precisely because the test project builds Main, whose
    post-build step copies into the game (the `/improve` hard rule 2 states the mechanism). Same defect in an agent-facing
    table: `docs/ai-includes/agent-operating-manual.md:43` gives the engine-binding gate as
    `dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "TestCategory=BindingVerification"` with no flags at all (lines
    41-42 above it at least carry a caveat).
  - **Setup script**: `README.md:55` `.\setup-dev-env.ps1  # configure BANNERLORD_GAME_DIR + dependencies`; the script
    only prompts for a path and sets the user-scope variable (`setup-dev-env.ps1:7,20`). No dependency step exists, and it
    does not set `core.hooksPath` (the natural home for DX-L6-01's one-time setup).
  - **Counts not in seed F8**: "50 careers" at `README.md:16,125` and `docs/features/career-system.md:148,173,174,290`
    against **67** `Career` elements in `Main/_Module/ModuleData/career_system/taom_careers.xml`; "39 GameModel overrides"
    at `README.md:15,149` against the 50 `Taom*Model` classes triage-check item 18 counted (the registry is gated,
    `lint_docs.py:1130`; the README number is not).
  - **Contradictions documented elsewhere in this file**: `release-process.md` step 1 versus `/release` Phase 1 on a dirty
    tree (DX-L6-04); `hooks-catalog.md:24,25,33,44` describing Stop reminders that cannot reach Claude (DX-L6-06); five docs
    calling Claude hooks "the pre-commit hook" (DX-L6-01).
- **Impact**: the README is the page a player, a contributor (five non-owner authors in `git shortlog`) and a fresh agent
  read first; two of its errors cost real time (a wrong game beta, a test run that overwrites the installed module or fails
  because the game holds the DLL).
- **Fix sketch**: drop exact counts from README (F8's option) and say "see the registries"; fix the three version
  lines, the branch sentence, the test command and the setup comment; fix `agent-operating-manual.md:43`. Make it stick
  with two linter checks: scan root `README.md` for a `Bannerlord v` claim that differs from the pin, and flag any
  `dotnet (build|test)` line in `README.md`, `docs/ai-includes/`, `.ai/` or `.agents/` that lacks `DisableModuleCopy=true`
  (the documented `./build.ps1` deploy path is exempt).
- **Effort**: S. **Risk**: LOW. **Confidence**: HIGH (each claim checked against the tree or GitHub this run).
- **Delta**: introduced (the 1.5.x line, the pin and the careers past 50 post-date `141b749`; triage-B L549 and seed F8
  hold the other README counts). **P1**: no. **Plan candidate**: yes, bundled with F8 (one README pass plus two lint rules).

## Harness audits: cite, do not re-run (deliverable 6)

- **Context budget**: the last re-baseline is `f31b108c` "docs(context-budget): re-baseline after eager-context diet"
  (2026-08-05), recorded in `docs/context-budget-baseline.md` (CLAUDE.md 4,717 tok, always-load rules 7 of 22 at 8,348 tok,
  eager subtotal about 16,841 tok excluding MCP). It predates ADR-011 (added 2026-09-23 in `c79a5852`), so its numbers are
  stale by construction; `baseline.md` says `lint_docs.py --fail-on-drift` now gates the ADR-011 budget in CI on every branch
  (`doc-budget.yml`), which supersedes a hand-run baseline. Not re-audited, per the brief.
- **Skill stocktake**: no run has left a record. `.claude/skills/skill-stocktake/SKILL.md` Phase 3 prints its report to the
  conversation and names no file to write; no commit subject since the skill landed names a stocktake run
  (`git log -i --grep=stocktake` returns only commits that edit the skill or cite it); `docs/audits/session-prompts.md:211`
  proposes "a `/skill-stocktake` cron schedule" that does not exist. So "when did it last run" is **unknown, and at least
  not since any durable record was possible**. The skill was last rewritten in `c79a5852` (2026-09-23). If its result is
  meant to count as evidence for `/improve` (playbook section 7 "run-or-cite them"), it needs a persisted output, for
  example `docs/audits/skill-stocktake-<date>.md`.

## Ranking (leverage = impact / effort, discounted by confidence and risk)

1. **DX-L6-03** (S, HIGH): one hook line plus a ruleset; guards pushed history on the live branch.
2. **DX-L6-06** (S, HIGH on mechanism): four Stop hooks the docs rely on are silent; tiny fix.
3. **DOCS-L6-08** (S, HIGH): wrong game beta and a deploying test command on the front page.
4. **DX-L6-02 Step 1** (S, HIGH): about 160 ms wall and 2 s CPU off every non-git Bash call; Step 2 later.
5. **DX-L6-01** (M, HIGH/MED): the gates bind every committer; green CI first. Unblocks DOCS-L6-07.
6. **DX-L6-04** (S for stamp and bundle, M for tag-only packaging): makes a player CTD traceable.
7. **DEPS-L6-05** option 3 plus F5 (S): 45% off each worktree; LFS is a decision item.
8. **DOCS-L6-07** (M, after DX-L6-01): removes the most contended file and its loss class.

## Considered and rejected

- **History rewrite to shrink the 3.27 GiB pack** (LFS migrate or filter-repo): breaks 26 tags, at least 833 cited SHAs,
  the SHA inside every shipped build and save; not worth it (DEPS-L6-05 option 2).
- **Gitignoring the generated `AssetSources/GauntletUI` atlases** outright: they are regenerable, but only on a machine
  with the Modding Kit's generator, and the Kit's texture compile reads them from disk; LFS is the safer lever.
- **Folding the slow gates (ModuleData 45 s, doc drift 20 s) into the dispatcher**: one registration's timeout would then
  have to cover their sum, and a kill silences every gate in it (`hook-authoring.md`); they keep their own registrations.
- **The `if:` handler field instead of a prefilter**: deferred by decision in `harness-facts.md` (per-gate proof needed).
- **Turning `build.yml` on for `bannerlord-1.5.x` right now**: it has failed 40 of 40 runs; enabling it before the three
  repairs adds a red badge, not a gate.
- **The 44 tracked DLLs under `Dependencies/_Module/bin/` (19 MB)**: the deliberate DR3 bundle, touched by 4 commits; not
  generated churn.
- **`docs/reference/troop-rosters.html` (4.5 MB, generated)**: tracked on purpose (`tools/generate_troop_roster_page.py:19`).
- **`.mcp.json` hardcoding `E:/repos/TAOM` (`:14,51,64`)**: a clone at another path loses the filesystem, moduledata and
  ElevenLabs servers, but the "two machines" layout is a recorded decision and the laptop path is not in evidence; LOW.
- **NuGet lockfiles**: held by triage-B L480/L488 (owner's decision), not re-raised.
- **Deleting the Claude subject hook today**: only after a committed `commit-msg` hook exists and `core.hooksPath` is set,
  or Claude commits lose their only gate.

## What I did not cover

- No `dotnet build` or `dotnet test` (brief). The MSBuild target names in DX-L6-04's design (`InitializeSourceControlInformation`,
  `AddSourceRevisionToInformationalVersion`) are UNVERIFIED; confirm with `-v:diag`.
- Hook timings are from Git Bash on this desktop, launched by a script, not by the Claude Code harness; the harness's own
  spawn overhead and its real parallelism were not measured. `suggest-compact.sh` was not run (it writes a counter).
- No live Stop was triggered, so DX-L6-06 rests on the repo's own DOC and EMPIRICAL visibility entries plus the scripts'
  output channel, not on an observed silent reminder.
- "The ten misses were IDE commits" is inferred from style and transcript absence; transcripts older than the retention
  window, or sessions on other machines (the laptop), were not searchable.
- GitHub LFS per-GiB prices come from a search snippet of the GitHub docs, not the full page; quotas and Actions
  bandwidth rules are from the fetched docs page.
- Worktree disk: measured one (`wt-opus-head`, 1,235 MB); the other eight were counted, not measured.
- I did not audit `.ai/`, `.agents/skills/`, `.codex/` or `tools/README.md` beyond the command grep (tools/README coverage is
  triage-B L579/L595). `docs/modding/README.md`'s "38 chapters" against 40 `.md` files was ambiguous (README plus a possible
  non-chapter file) and is not reported.
- Instruction weight and context budget were not re-measured (brief: ADR-011 reset them today).
- No GitHub issue or PR state beyond branch protection, rulesets, runners, variables and the Actions run list.


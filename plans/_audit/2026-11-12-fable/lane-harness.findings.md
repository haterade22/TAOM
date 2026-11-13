# Lane harness: findings (2026-11-12-fable)

Baseline `7087fa633`, worktree `wt-baseline`. Read-only lane. 11 findings, ranked by leverage.

## Measurements

All in `wt-baseline` at `7087fa633`.

- **Always-on text (every session and every custom or general-purpose spawn).** `wc -c`: CLAUDE.md
  8,270, AGENTS.md 7,772, orientation.md 9,838 (25,880 raw; `lint_docs.py` counts 25,590 with CRLF
  folded). The seven rules with no `paths:` (classified by a front-matter loop over `.claude/rules/*.md`):
  environment-failures 1,237, evidence-over-claims 3,820, model-effort-selection 2,291, output-style
  3,881, simplicity-criterion 1,057, think-before-coding 2,438, working-discipline 1,902, total 16,626
  raw (`lint_docs.py`: 16,370). Lines: 114 + 95 + 81.
- **Headroom against the hard caps.** `python -B tools/lint_docs.py --fail-on-drift`: "CLAUDE.md with its
  imports is 25,590 B, approaching the 25,600 B budget" (10 B left) and "rules without paths: total
  16,370 B, approaching the 16,384 B budget" (14 B left). Exit 0, `total_findings: 5`, all `size-warn`.
- **MEMORY.md** (machine-local, loads in every main session and, per harness-facts.md:31, in every
  sampled subagent spawn): `wc -c -l` 34 lines, 4,257 B, over the ADR-011 4 KB target.
- **`bash tools/context_budget_scan.sh`** (read the source first: it writes nothing; run with
  `CLAUDE_PROJECT_DIR` on the worktree, exit 0): eager baseline about 13,347 tokens; per custom-agent
  spawn about 10,488 tokens; skills 23 (994 eager tokens), agents 6 (306), MCP 4 (495 names). Issues: the
  three path rules over 12,288 B (gui-ui 16,699, harness-facts 21,456, hook-authoring 13,404) and the
  MEMORY.md size.
- **Skill descriptions.** An awk word count of each `description:` line: the longest is `game-run` at
  30 words; none is over the 30-word cap.
- **Hooks per tool call** (`.claude/settings.json` parsed with Python): a Bash call runs 10 PreToolUse
  scripts (nine `Bash|PowerShell`, plus `check-bash-length.sh`) and one PostToolUse
  (`mark-verification-run.sh`): 11 bash starts. A PowerShell call runs 9 plus 1. An Edit or Write runs
  `config-protection.sh` before and `check-polearm-shield-parity.sh` and `check-feedback-memory.sh` after:
  3 bash starts. Every SubagentStart runs `log-agent.sh`, every Stop runs `check-verification-evidence.sh`,
  every session start runs three scripts. Every Bash and Edit hook has a bash-builtin raw-payload
  prefilter before `_pybin.sh` is sourced (grep of `INPUT=$(cat)` and the first `[[ "$INPUT" == ...` line
  in each script), so a call without the trigger text starts no Python and no git. Plan 013 is complete
  for every registered hook; no finding there.
- **Edit-hook timing** (dry payload: an Edit of `docs/INDEX.md`, which every hook exits on; the way
  `tools/test_hooks.sh:3373` pipes a payload into `check-polearm-shield-parity.sh`; script
  `scratch\lane-harness\time_edit_hooks.sh`, three runs of five): `check-polearm-shield-parity.sh` 163,
  168, 181 ms; `config-protection.sh` 81, 77, 77 ms; `check-feedback-memory.sh` 134 (first run), 81, 77
  ms; a bare `bash` that reads stdin and exits 61, 62, 65 ms.
- **Stop-hook git cost** (worktree, clean, 13,743 tracked files; three runs): the four git reads
  `check-verification-evidence.sh` makes on every Stop (`rev-parse`, `diff --name-only`,
  `diff --cached --name-only`, `ls-files --others --exclude-standard`) took 295, 317, 267 ms;
  `git status --porcelain -uall -- '*.cs'` took 98, 122, 89 ms.
- **Banner counts** (script `scratch\lane-harness\banner_count.py`: the top-level `*.jsonl` main-session
  transcripts under `~/.claude/projects/e--repos-TAOM` modified in the last 7 days, 69 files; a hit is any
  transcript line holding the text, so a session that only discussed a banner counts too):
  `WORKTREES: ` in 24 sessions, a later tool call naming `worktree_audit.py` in 1;
  `graphify code graph: STALE` in 68; `graphify code graph: current` in 1; `NOTE: MEMORY.md is ` in 2.
  The UPSTREAM AHEAD and ARMORY ART DRIFT counts from this script are not usable (CLAUDE.md, which every
  transcript carries, contains both strings); the retro's own counts are cited instead.
- **Stale references.** Script `scratch\lane-harness\refcheck.py` over AGENTS.md, CLAUDE.md,
  `docs/ai-includes/*.md`, `.claude/rules`, `.claude/skills/**`, `.claude/agents`, `.ai/**`, `.agents/**`,
  `.codex/*`, `.github/**` (117 files): every repo path that is not tracked, and every `--flag` after a
  `python tools/X.py` that does not appear in that tool's source. 84 path hits, all of them placeholders
  (`Main/Features/X`, `vX.Y.Z`), prefixes (`tools/generate_`), output folders (`docs/reviews/raw`),
  external repos (`docs/engine/modding-kit.md` is yotthani's) or `git show` history; 6 flag hits, all
  `reviewctl.py --builder-*`, which `tools/reviewctl.py:385` builds as `"--builder-" + key`. The files the
  CLAUDE.md Skills table names are all tracked (`git ls-files --error-unmatch`, 18 paths). Result: no
  stale command, file or flag in the scanned scope. Subcommands and PowerShell `-Param` names were not
  checked; I read source instead of running `--help`, because several tools write on a plain run.
- **Installed Claude Code:** the newest main transcript's `version` fields run up to `2.1.322`.

## Findings (ranked by leverage)

### [DX-01] Let /ship's final /verify reuse the first run when the tree has not changed

- **Evidence**: `.claude/skills/ship/SKILL.md:19` runs `/verify` in Phase 1 and `:31` runs it again in
  Phase 4 with no condition. `.claude/rules/evidence-over-claims.md:40-41`: "A verification you ran this
  turn and have not invalidated is still evidence; re-running it adds none." `docs/ai-includes/completion-workflow.md:74,79`
  repeats the two runs. One full `/verify` (`.claude/skills/verify/SKILL.md:19,30,42,57,76,87,100`) is a
  build, a forced NuGet restore, the C# suite, the Python suite, the canary, the API snapshot and the data
  gates. Baseline (`baseline.md`): C# suite 129 s wall, Python suite 370 s, build 6 s.
- **Impact**: a `/ship` whose review finds nothing to fix pays about 500 s of suite time twice on an
  identical tree. After `/ship`, `/release` runs `/verify` once more (`release/SKILL.md:57`), but its
  version bump changes the tree, so that run is not a duplicate.
- **Effort**: S.
- **Risk**: LOW. The verdict is the same by construction when the inputs are the same; the risk is a
  wrong "unchanged" test, so the test must include untracked files and live-module edits.
- **Confidence**: HIGH (read both skills and the rule).
- **Fix sketch**: Phase 1 records `git rev-parse HEAD`, a hash of `git diff HEAD --binary` and of
  `git ls-files --others --exclude-standard` (with each file's hash), and the time. Phase 4 recomputes them;
  if all match and no live `TAOM_Map` or Armory file is newer than that time (the `find -newermt` that
  `deep-review/SKILL.md:56` already uses), it cites the Phase 1 report instead of re-running; otherwise it
  runs `/verify` as now. Same text in `completion-workflow.md` step 5.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes; a skill-text change with a clean before and after story, no gate removed.
- **Issue**: none.

### [DX-02] Make /review-codex 3f run the filtered tests per fix, not the full suite per bug

- **Evidence**: `.claude/skills/review-codex/SKILL.md:234-237`: "For each confirmed bug: ... `dotnet build
  TAOM.Tests` ... `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: must pass". The
  always-on cadence rule, `.claude/rules/evidence-over-claims.md:35-39`: a compile after each edit, "the
  filtered suite while iterating (`python tools/graphify_taom.py tests ...` prints its `dotnet test
  --filter` line)", and the full suite "at each work-item boundary". `/ship` Phase 4 (`ship/SKILL.md:31`)
  runs the full suite after the fixes anyway.
- **Impact**: N confirmed bugs cost N full C# suites (129 s each at the baseline) before the Phase 4
  run repeats it. The two documents give an agent opposite instructions for the same step.
- **Effort**: S.
- **Risk**: LOW. The full suite still runs once after the last fix (Phase 4), so no failure can pass
  that passes today.
- **Confidence**: HIGH.
- **Fix sketch**: 3f step 3 becomes "the filtered run `graphify_taom.py tests <changed type>` prints; the
  full suite runs once after the last fix (`/verify`)". Keep step 2's compile.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes; text alignment with the always-on rule.
- **Issue**: none.

### [DX-03] Recover headroom under the two always-on caps by deleting text an always-on file already holds

- **Evidence**: `lint_docs.py --fail-on-drift`: entry docs 25,590 of 25,600 B, unscoped rules 16,370 of
  16,384 B, so the next 11-byte or 15-byte addition to either fails `.githooks/pre-commit` and the
  Claude commit hook. Text stated twice inside the always-on set:
  - `CLAUDE.md:98-102` ("Model & Effort Selection", 312 B by a section split) points at
    `.claude/rules/model-effort-selection.md`, which is itself always loaded, and repeats its tier
    names (`model-effort-selection.md:9-11`).
  - `CLAUDE.md:93` "so never pass `model`" repeats `model-effort-selection.md:30` ("Never pass `model` to
    `deep-reviewer`"); the deep-review skill, the only place that spawns it, says it a third time
    (`deep-review/SKILL.md:16`).
  - `.claude/rules/output-style.md:55-57`, the "**Also:**" list (470 B), is a copy of
    `docs/reference/writing-tells.md` (rows 12 and 18 and lines 101-102 hold the same words:
    testament, serves as, substrate, wedge, "in order to"), and `output-style.md:58` already links that
    doc. ADR-011 "Never put these in the first three tiers: ... copies of text that lives in another tier".
- **Impact**: about 400 B back under the entry cap and 470 B under the rules cap with no rule lost;
  every session and every custom spawn reads about 870 B less (`context_budget_scan.sh`: about 10,488
  tokens per custom spawn today).
- **Effort**: S.
- **Risk**: LOW. Each deleted line has its live copy in a file that loads in the same contexts
  (the writing-tells list loads on demand only, so the "Also" items leave the always-on set; the ranked
  tells 1 to 5 stay).
- **Confidence**: HIGH for the byte counts; MED that the "Also" list adds nothing to chat adherence
  (not measurable without an eval).
- **Fix sketch**: delete `CLAUDE.md` "## Model & Effort Selection" (keep the tier agent names in the
  Subagents bullet, which already lists them), drop the parenthetical at `CLAUDE.md:93`, and replace the
  "**Also:**" paragraph with nothing (the "Full list" link at `output-style.md:58` stays). Optional second
  step: the Skills-table rows that only repeat a skill description (`CLAUDE.md:77` `/harness-retro`, empty
  Gate; `CLAUDE.md:74` `/adopt-external`/`/upstream-review`), 220 B; the skill listing is re-sent after
  compaction from v2.1.304 (`harness-facts.md:34`) and 2.1.322 is installed. That step changes what
  `tools/run_skill_evals.py` stages (`run_skill_evals.py:5`, case `tools/evals/route-harness-retro`), so its
  proof is a paid eval run on the maintainer's word; leave it out of an unattended plan.
- **Delta**: pre-existing (the caps were reached by growth since the 2026-09-23 "harness diet" rejection
  in `plans/README.md:107`, which measured about 34 KB; today it is 41,960 B by the linter).
- **P1**: no.
- **Plan candidate**: yes for the three deletions; the Skills-table step only with a paid eval.
- **Issue**: none.

### [DX-04] Remove the silent SubagentStart logger that nothing reads (Step 0 for the maintainer)

- **Evidence**: `.claude/settings.json:169` registers `.claude/hooks/log-agent.sh` on every SubagentStart.
  It prints nothing and appends to `.claude/logs/agent-audit.log` (`log-agent.sh:19,30`). `git grep
  agent-audit` over `tools`, `.claude` and `docs/reviews`: no reader; `deep-review/SKILL.md:56` tells
  sessions not to rely on it, and the one tool that once read it (`check-deep-review.sh`) is gone
  (`docs/reviews/lessons/build-tooling-workflow.md:266`). The subagent transcripts already hold every
  spawn's type (`harness_retro.py` counts spawns from them: retro 2026-11-10 lines 9, 59). ADR-011
  "Hooks": "A hook exists only if it gates (deny or ask, exit 2) or prints on a channel Claude sees."
  Per spawn, with no `jq` on PATH (`log-agent.sh:7-16`): two `echo|grep|head|sed` pipelines, `mkdir`,
  `wc`, `date`: about 11 process starts. 1,183 subagent transcripts in the week to 2026-11-10 (retro line 9).
- **Impact**: about 13,000 process starts a week that produce a file no one reads, and one hook to keep
  under `test_hooks.sh`'s timeout and anchor checks.
- **Effort**: S.
- **Risk**: LOW. Not a gate. The maintainer may read the log by hand: ask first.
- **Confidence**: HIGH for "no reader in the repo"; UNVERIFIED whether the maintainer reads it.
- **Fix sketch**: Step 0 for the maintainer: delete the `SubagentStart` block from `.claude/settings.json`
  (the one entry running `log-agent.sh`). Then an executor deletes `.claude/hooks/log-agent.sh`, its row in
  `docs/reference/hooks-catalog.md:39`, the `agent-audit.log` cleanup in `tools/test_hooks.sh:1411`, and
  repoints the two comments that cite it as an example (`hook-authoring.md:179`,
  `check-polearm-shield-parity.sh:33`).
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes, after the maintainer's Step 0 and his word that he does not read the log.
- **Issue**: none.

### [DX-05] Parse the edited path in check-polearm-shield-parity.sh with a bash regex

- **Evidence**: `.claude/hooks/check-polearm-shield-parity.sh:35-41`: with no `jq` the path comes from
  `echo | grep -oE | head -1 | sed | sed` on every Edit and Write, before the relevance filter at
  `:52-61`. Timing above: 163 to 181 ms against 77 to 81 ms for the two sibling Edit hooks, which parse
  in bash. `check-verification-evidence.sh:46-54` already reads a JSON string field with `[[ =~ ]]` and
  `BASH_REMATCH`, unescaping the doubled backslashes in-process.
- **Impact**: the three PostToolUse/PreToolUse Edit hooks run in parallel, so this hook sets the wall
  time after every Edit and Write: about 90 ms more than it needs, on this desktop.
- **Effort**: S.
- **Risk**: LOW. Same field, same unescape; `tools/test_hooks.sh:3373-3384` exercises the FAIL and
  quiet paths, and a case with a Windows path and one with spaces would pin the parse.
- **Confidence**: HIGH for the timing on this desktop; the count of Edit and Write calls per session is
  not measured here.
- **Fix sketch**: replace `:35-41` with the `FILE_RE` and `BASH_REMATCH` pattern of
  `check-verification-evidence.sh:46-54` for `"file_path"`, keeping the `jq` branch. Run the hook suite.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes.
- **Issue**: none.

### [DX-06] Cut session-start lines that repeat the harness's own context or contradict each other

- **Evidence**:
  - `.claude/hooks/session-start.sh:61-63` prints `Branch:` and `:360-363` the last five commits. The
    harness already injects the branch, the status and the last five commits as `gitStatus` into every
    main session and again after `/compact` (`harness-facts.md:31,34`; this session's own context shows
    "Current branch", "Status" and "Recent commits" with five lines). Two git process starts per session.
  - `:202-226`: when the graph is stale, `graphify_taom.py status --brief` prints a "refresh: python
    tools/graphify_taom.py refresh (about 2.5 minutes; run it in the background)" line
    (`tools/graphify_taom.py:466-468`), and the hook then starts that same refresh itself
    (`session-start.sh:214-222`). The STALE line was in 68 of 69 transcripts last week, so nearly every
    session is told to start a refresh that is already running.
  - `:255-289`: two worktree checks. The first counts every worktree and fires over 15; the second counts
    `.claude/worktrees/` directories, which the first already includes. `git -C <repo> worktree list`
    shows 41 worktrees today, so WORKTREES prints at every start (24 of 69 transcripts last week, the
    banner being younger than the window, with one later `worktree_audit.py` call).
- **Impact**: a few hundred bytes and four process starts per session, and one instruction per session
  that the next line cancels. Small; it is ranked for its contradiction, not its cost.
- **Effort**: S.
- **Risk**: LOW for the first two parts. `tools/test_hooks.sh` pins several of these outputs
  (`ss_retro` at `tools/test_hooks.sh:3562`, `ss_wt` at `:3618`), so the suite needs matching edits.
- **Confidence**: HIGH.
- **Fix sketch**: delete the `Branch:` and "Recent commits" blocks (keep the Uncommitted counts, which the
  harness list does not total); in the stale branch, print the STALE line without the "refresh:" hint
  when the detached refresh starts (a `--no-hint` flag on `status`, or `head -1`); fold the
  `.claude/worktrees/` count into the WORKTREES line. How often WORKTREES prints at 41 worktrees (a stamp
  file, like retro proposal P2 for UPSTREAM AHEAD) is the maintainer's call.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes for the deletions and the hint; no (decision) for the WORKTREES frequency.
- **Issue**: none.

### [DX-07] Resolve which rule governs opening GitHub issues unasked

- **Evidence**: `.claude/rules/think-before-coding.md:25-26` (added 2026-11-12 in `f5fa5ce91`): "Standing
  word (Mike, 2026-11-12): do what you recommend, repo-required issues too ... The git, review-gate,
  paid-AI and deletion limits above stay" (public actions are not in that list). `CLAUDE.md:80`: "Never
  auto-invoke | `/review-codex` (paid), `/issue` (public)". `.claude/skills/ship/SKILL.md:32`: the issue is
  "opened on Mike's word when work starts". `AGENTS.md:79` asks once without a standing word. Two
  always-on files give opposite answers.
- **Impact**: an agent that obeys one breaks the other; the issue backlog the documentation duty needs
  depends on which it picks.
- **Effort**: S.
- **Risk**: LOW to edit; the choice changes when public actions happen.
- **Confidence**: HIGH that they conflict.
- **Fix sketch**: once the maintainer says which holds, change the CLAUDE.md row to "`/issue` (public):
  on the standing word in think-before-coding.md" or narrow the standing word; align `ship/SKILL.md:32`.
- **Delta**: introduced (2026-11-12).
- **P1**: no.
- **Plan candidate**: no (decision): it changes when a public action runs.
- **Issue**: none.

### [DX-08] Align /ship's Codex phases with /deep-review's own Codex pass and fix-loop rule

- **Evidence**: `/deep-review` Step 0 already dispatches Codex on the diff at tier 1 and 2
  (`deep-review/SKILL.md:22-49`), and `:264` re-reviews a fix only "when it fixed a CRITICAL or HIGH ...:
  one Codex pass at `high` plus the lens that found it". `completion-workflow.md:77-78` points to that rule.
  `/ship` then runs `/review-codex` again in Phase 2 (`ship/SKILL.md:23-25`) and a third time in Phase 3
  on every post-fix diff (`:27-28`), under "do not skip any" (`:16`). `ship/SKILL.md:14` also skips the
  whole sequence for "one-line C# fixes" because "6+ review agents plus Codex" is wasteful, while
  `CLAUDE.md:59` requires `/deep-review` on "every such commit; skip only config and docs" and the tier
  table now launches one to four lenses (`deep-review/SKILL.md:74-76`).
- **Impact**: up to three paid Codex runs on one change where the review skill specifies one, plus a
  second on a CRITICAL or HIGH fix; and a one-line C# fix that follows `/ship`'s skip line gets no review.
- **Effort**: S.
- **Risk**: MED: it changes how many independent reviews a change gets.
- **Confidence**: HIGH that the texts disagree.
- **Fix sketch**: Phase 2 becomes "the Codex pass of `/deep-review` Step 0 counts; run `/review-codex`
  only when Step 0 did not run"; Phase 3 follows `deep-review/SKILL.md:264`; line 14 says a one-line C# fix
  skips `/ship` but still gets `/deep-review` at its tier.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: no (decision): it lowers the number of review passes `/ship` mandates.
- **Issue**: none.

### [DX-09] Correct /verify's stale counts and raise its discovery floors

- **Evidence**: `.claude/skills/verify/SKILL.md:52` "A total under 20,000 (today about 31,700)"; baseline
  total 39,259. `:62-63` "About four minutes"; baseline 363 s (370 s wall). `:65` "fewer than 2,500 tests";
  baseline `Ran 8578 tests`. `.ai/verification.md:55` repeats the 2,500 floor. `:57` runs `python -m
  unittest` without `-B`, while the run rules require `-B` so no `__pycache__` lands beside the hooks.
- **Impact**: a discovery break that loses 70% of the Python tests (8,578 to 2,600) still passes the
  floor; the "today" figure is a count nothing computes (CLAUDE.md "Never put counts nothing computes").
- **Effort**: S.
- **Risk**: LOW. Raising a floor makes the gate stricter, never looser.
- **Confidence**: HIGH.
- **Fix sketch**: drop "today about 31,700" and "About four minutes"; set the floors near 80% of the
  baseline (for example 30,000 and 6,500) in both files; add `-B` to Step 2b.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes.
- **Issue**: none.

### [DX-10] Read the Stop hook's dirty C# files with one git call

- **Evidence**: `.claude/hooks/check-verification-evidence.sh:52,56-58` runs `rev-parse`, `diff
  --name-only`, `diff --cached --name-only` and `ls-files --others --exclude-standard` at every turn end,
  then filters for `*.cs` in bash (`:66-78`). Timing above: 267 to 317 ms for the four, 89 to 122 ms for
  `git status --porcelain -uall -- '*.cs'` on the same tree.
- **Impact**: about 0.2 s at the end of every turn in every session on this desktop; more on the main
  checkout, whose untracked scan is larger (not measured: I did not run git in the main checkout).
- **Effort**: S.
- **Risk**: LOW to MED: the porcelain parse must keep staged deletions out (`:70-72`) and read rename
  lines (`R  old -> new`) as the new name; `tools/test_hooks.sh` 7a covers the reminder.
- **Confidence**: HIGH for the timing; retro proposal P1 (`docs/reviews/harness-retro-2026-11-10.md:130`,
  pending the maintainer) would rewrite this hook to judge the session's own edits, which makes this
  finding moot if accepted.
- **Fix sketch**: replace the three lists with one `git status --porcelain=v1 -uall -- '*.cs'` and a
  bash loop over its lines; do it only if P1 is declined, or inside P1.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes, sequenced after the P1 decision.
- **Issue**: none.

### [DX-11] Trim the machine-local MEMORY.md lines that repeat repo rules (maintainer's file)

- **Evidence**: `~/.claude/projects/e--repos-TAOM/memory/MEMORY.md` is 4,257 B (cap 4,096; session-start
  prints its NOTE). Its line 24 (`model-effort-free-choice`: "pick subagent model and effort without
  asking") repeats `.claude/rules/model-effort-selection.md:27`; line 25 (`act-on-recommendations`: "do
  what I recommend, never ask") repeats `think-before-coding.md:25`. ADR-011 "Memory": resume cards and
  machine-local paths only. MEMORY.md reaches every sampled subagent spawn (`harness-facts.md:31`).
- **Impact**: about 200 B per main session and per spawn, and the file back under its cap so the
  session-start NOTE stops.
- **Effort**: S.
- **Risk**: LOW.
- **Confidence**: HIGH for the duplication; the cards' own text was not read.
- **Fix sketch**: the maintainer deletes the two index lines (and their card files if nothing else in
  them is machine-local). No executor edits memory.
- **Delta**: introduced since the 2026-11-10 retro (3,923 B then).
- **P1**: no.
- **Plan candidate**: no; machine-local file outside the repo.
- **Issue**: none.

## Already owned elsewhere (not reported as new)

- **Commit gates run twice on a Claude commit** (`lint_docs --drift-only` from
  `check-doc-config-drift.sh` and `.githooks/pre-commit`; `validate_moduledata` from
  `check-moduledata-validation.sh` and the git hook, about 16 s each by the baseline): stage two in
  `docs/features/commit-gates.md:44-46`, due about 2026-11-17.
- **UPSTREAM AHEAD every session** (49 sessions, none acted): retro 2026-11-10 proposal P2, pending.
- **ARMORY ART DRIFT every session** (43 sessions): the retro decided the cure is a catalogue regen.
- **check-verification-evidence false reminders**: retro proposal P1, pending (see DX-10).
- **The deep-review tier table's hole** (plain service C# is in no tier): retro "Owed", D4.

## Considered and rejected

- A single dispatcher for the nine Bash gates: rejected in `plans/README.md:102`; the prefilters hold
  (every Bash call starts no Python or git when its text cannot concern a gate).
- `detect-docs-gaps.sh` reimplements `lint_docs.py check_missing_feature_docs` (`lint_docs.py:784-805`)
  in bash with the same fuzzy rule: two copies, but the linter's check is not on the commit path
  (`--drift-only` skips it, `lint_docs.py:2093-2097`), so deleting the hook removes the only automatic
  signal, and it prints nothing today. Not worth a plan.
- `mark-verification-run.sh` registered twice (Bash, PowerShell) instead of one `Bash|PowerShell`
  matcher: each call matches one registration, so no cost.
- Skill descriptions over 30 words: none (longest 30).
- `statusline.sh` cost: one `git status` per repaint; the VS Code panel does not show it but runs it
  (`harness-facts.md:101`). Dropping `statusLine` is the maintainer's settings choice and was not measured.
- The session-start auto-refresh of the code graph (stale at 68 of 69 starts): decided in #995.

## Note for the maintainer (environment, not a repo finding)

- The main clone's `core.hooksPath` is the absolute `e:\repos\TAOM\.githooks` (`git -C E:/repos/TAOM
  config core.hooksPath`), while `setup-dev-env.ps1:25` sets the relative `.githooks`. With the absolute
  value every worktree runs the main checkout's copy of the git hooks (including any uncommitted edit
  there), not its own; with the relative value each worktree runs its own. Report only.

## What I did not cover

- I did not run `tools/test_hooks.sh` (the orchestrator runs it) or `python -B tools/audit_claude_config.py`.
- I did not time any PreToolUse Bash gate, SessionStart hook or the status line: `session-start.sh`
  starts a detached graph refresh and a native-DLL archive on a startup payload, and `upstream-watch.sh`
  writes a cache, so none of them is read-only. Their cost is counted in process starts only.
- I did not count Edit, Write or Bash calls per session, so the per-call costs in DX-05 and DX-10 are not
  multiplied out.
- I did not read every skill body in full: verify, ship, deep-review (first 100 lines and line 264),
  review-codex (3f to 3i) and the dispatch rules were read; engine-bump, release, investigate,
  native-crash-triage, security-scan, game-run, improve and the lens files were only scanned by the
  reference checker.
- `.ai/review-reference.md` (40 KB), `docs/ai-includes/codex-operating-guide.md` and the `.agents`
  skills were checked for stale paths only, not read for contradictions with the Claude layer.
- Path-scoped rule sizes (gui-ui, harness-facts, hook-authoring over 12 KB) are reported by the linter
  and the scan; I did not look for what to move out of them.
- `.github/` holds no workflows; nothing there was audited beyond paths.

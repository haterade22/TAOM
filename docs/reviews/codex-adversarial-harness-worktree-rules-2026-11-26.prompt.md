# Codex adversarial review: harness tooling of commit 0e92a28f9 (2026-11-26)

You are the reviewer (.ai/roles/reviewer.md). This is a review only: change no file, run no git command that writes, start no paid tool. Return the full report as your FINAL MESSAGE. Do NOT write the report to any file; the dispatcher captures stdout.

## What was built

Commit 0e92a28f9 on bannerlord-1.5.x (already merged) changed the Claude Code harness of this repo. No C#, no XML. Review only the scripts and their tests, not the instruction text. Read the change with: git show 0e92a28f9 -- .claude/hooks .claude/settings.json tools

1. .claude/hooks/point-worktree-rules.sh (new, 249 lines). Registered in .claude/settings.json for PostToolUse (matcher Read|Edit|Write) and SessionStart. A path rule (.claude/rules/*.md with a paths: list) loads only for files in the checkout the session started in. For a file in a sibling checkout of the same repository, the hook prints additionalContext that names the rule files whose globs match the file, once per rule file and session. On SessionStart it forgets the session's notes.
2. .claude/hooks/session-start.sh and .claude/hooks/_session_ledger.sh: the ARMORY ART DRIFT banner prints once per drift state and per checkout, by a stamp file .claude/logs/.armory-drift-printed. In a linked worktree with no newer pack it prints an UNCHECKED note.
3. tools/lint_docs.py: a new cap SKILL_BODY_MAX_BYTES (20,480 B) per .claude/skills/<name>/SKILL.md. Over the cap is a warning (size-warn); it fails (kind skill-size) only when the file is larger than its copy at the base. check_context_budget now takes a base, so a run with no commit view (CI with --dash-base HEAD^1) measures growth against that base.
4. tools/harness_retro.py: a "Rule delivery by checkout" table and a "rule reach:" line, from the session transcripts.
5. Tests: tools/test_hooks.sh sections 7r (point-worktree-rules) and 7n (Armory banner), tools/tests/test_lint_docs.py, tools/tests/test_harness_retro.py.

## READ FIRST

- docs/reviews/rca-armory-drift-fresh-checkout-2026-11-25.md (the Claude review of this change: findings T1 to T8, S1 to S5, E1 to E3, and what was not applied)
- docs/reviews/harness-retro-2026-11-25.md (why the hook exists, proposals P1 to P3)
- .claude/rules/hook-authoring.md and .claude/rules/harness-facts.md (the repo's rules for hooks: fail open, timeouts, stdin, Git Bash on Windows, CRLF)
- docs/features/doc-health-linter.md (check 7 and the caps table)

## Known Suspects (CONFIRM or DISPUTE each, with file:line and the lines you read)

S1. Glob matching in point-worktree-rules.sh. The hook implements its own match of a rule's paths: globs (**, *, braces, quotes, a leading slash, a Windows drive path, backslashes) against the file path. Hypothesis: there is a glob form used by a rule file in .claude/rules/ that the hook matches differently from Claude Code's own loader, so a rule is named for a file it does not cover, or not named for a file it covers. Enumerate every distinct glob form in .claude/rules/**/*.md and test each against the hook's matcher.
S2. Which checkout. The hook must stay silent for a file in the session's own checkout, for a file outside any checkout of this repository, and for a file in a different repository, and it must speak for a sibling worktree. Hypothesis: a path form (a lower-case drive letter, /e/ against E:/, a symlink or junction, a trailing slash, a path with spaces, a UNC path, a file that does not exist yet on a Write) puts a file in the wrong class.
S3. The once-per-session ledger. Hypothesis: two tool calls that end at the same time, a session id with unusual characters, a missing session id, or a SessionStart with source compact or resume leaves the ledger wrong (a note repeated on every call, or a note never sent again after a compaction dropped the rule from context).
S4. Fail open and cost. The hook runs after EVERY Read, Edit and Write in every session with a 5 s timeout. Hypothesis: there is an input (a very large payload, a binary path, a payload with no tool_input, malformed JSON, no python on PATH) where it exits non-zero, prints to stdout something that is not valid hook JSON, or takes over 5 s. State the measured or reasoned cost of the common path (a file in the session's own checkout).
S5. The Armory stamp in session-start.sh. Hypothesis: there is a sequence of states (drift, banner printed, audit run and catalogue regenerated, a second different drift, a worktree made later, the stamp older than seven days) where the banner stays silent although real drift exists and was never shown, or where it prints every session again.
S6. The growth gates in lint_docs.py. Hypothesis: a commit can grow an over-cap SKILL.md or path rule and pass, through a rename of the skill folder, a new file that starts over the cap, a base that does not hold the file, --staged against an amend, CRLF against LF byte counts, or a merge commit. Also check that an unresolvable base does not turn the gate off silently.
S7. The tests. Hypothesis: a row of tools/test_hooks.sh 7r or 7n, or a new unit test, passes whether or not the code under test is correct (it asserts on its own fixture, or its failure branch cannot be reached). Name the rows.

## Files

Hooks: .claude/hooks/point-worktree-rules.sh, .claude/hooks/session-start.sh, .claude/hooks/_session_ledger.sh, .claude/hooks/_pybin.sh (helpers the new hook uses), .claude/settings.json
Tools: tools/lint_docs.py, tools/harness_retro.py
Tests: tools/test_hooks.sh (sections 7n and 7r), tools/tests/test_lint_docs.py, tools/tests/test_harness_retro.py
Rule files the hook reads: .claude/rules/**/*.md (their paths: frontmatter only)

## REQUIRED SECTIONS

1. KNOWN SUSPECTS: one verdict per suspect, CONFIRMED or DISPUTED, each with file:line and the exact lines.
2. TRACE: walk point-worktree-rules.sh top to bottom for these four payloads and state its output and exit code for each: a Read of a .cs file in the session's own checkout; a Read of Main/IoC.cs in a sibling worktree; an Edit of a file outside the repository; a SessionStart payload.
3. GATE TRACE: walk check_context_budget in tools/lint_docs.py for a commit that grows .claude/skills/deep-review/SKILL.md from 20,296 B to 20,600 B, once with --staged and once with --dash-base HEAD^1 and no commit view.
4. FINDINGS: every defect you can show, most severe first. For each: severity (P1 a wrong result or a blocked session, P2 a gate that can be passed or a note that misleads, P3 the rest), file:line, the concrete input that shows it, and the wrong result. A finding with no concrete input is an OBSERVATION, listed apart.
5. NOT CHECKED: what you could not run or read, stated plainly. Do not guess the behaviour of Claude Code's own rule loader: say UNVERIFIED where you have no evidence.

## QUALITY GATES

- Quote the lines you rely on. No finding from a function name alone.
- You may run read-only commands (bash tools/test_hooks.sh, python -m unittest tools.tests.test_lint_docs, a hook fed a payload on stdin with HOME and CLAUDE_PROJECT_DIR pointed at a temp folder). Do not edit files in the checkout. Do not run git add, commit, stash, reset, checkout or clean.
- Windows: the shell is Git Bash, the files are CRLF in the working tree for .md and .py and LF for .sh. Use python, never python3.
- Do not report the items the RCA already lists as fixed unless the fix is wrong; say so when it is.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

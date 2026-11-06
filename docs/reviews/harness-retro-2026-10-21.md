# Harness retro, 2026-10-21 (baseline)

The first run of [`/harness-retro`](../ai-includes/harness-retro.md), adopted from Harness² the
same day ([review](adopt-harness2-2026-10-21.md), #962). With no earlier retro, this one is a
baseline: it covers the whole transcript history on this machine and gives verdicts only where a
signal measures a change directly.

## Window and coverage

- **Window:** 2026-09-21 (the oldest transcript kept) to 2026-10-21.
- **Transcripts, at the last run:** 128 main sessions and 1,729 subagent transcripts, 2,082,921
  records, 0 parse failures, 64 compactions. `python tools/harness_retro.py --since 2026-09-21`,
  about 30 s.
- **Reviewer accuracy** (`tools/analyze_reviews.py --summary --no-plot`): 55 reviews in the parsed
  `REVIEW-LOG.md` tables, 146 real bugs, 18 false positives, 18 misses (89% accuracy). The parser
  reads only the Summary and Gap Reviews tables, so recent rows may be missing; not judged here.
- **Miner defects found and fixed during this retro** (tests first). Found while reading the
  first report: `hook_blocking_error` nests its reason one dict deep, so every Stop block had read
  as hook "Stop" and `check-version-tagged` was listed with no fires; grouped rows had no dates, so
  a problem a later commit fixed looked current. Found by the code review: parallel calls sent in
  one message were counted as a retry loop (18 "runaways" became 3), and gates that can only ask
  were listed as never firing, although an approved ask leaves no trace.

## Signals

| Signal | Count | Sessions | Dates | Example session |
|---|---|---|---|---|
| `detect-docs-gaps.sh` killed at its 10 s timeout | 39 | 39 | 09-23 to 10-21; nearly every start since 10-17 | `008da7fd` |
| PreToolUse gates killed at their 5 s timeout (all gates) | about 400 of about 1,000,000 runs | 26 for the worst gate | bursts: 89 on 09-23, 45 on 09-27, 42 on 10-08, 134 on 10-18; last 10-18 | `1412750d` |
| of which `block-dangerous-git` | 88 | 26 | 09-21 to 10-18 | `1412750d` |
| of which `block-broad-git-add` / `check-claude-files-tracked` / `block-no-verify` / `validate-push` | 87 / 75 / 37 / 25 | 26 / 24 / 21 / 13 | to 10-18 | `1412750d` |
| `session-start.sh` killed at its 10 s timeout | 12 | 12 | to 10-18 (5 that day); none after | |
| Bash quoting failure ("unexpected EOF while looking for matching") | 151 | 37 + 14 | whole window | `09232f6e` |
| `UnicodeEncodeError: 'charmap'` from Python output | 43 | 15 + 12 | whole window | `4c7e68bd`, `09232f6e` |
| `deep-reviewer` tried Write, which it does not have | 66 of the 70 refused Write calls (architect 3, fast-reader 1) | 22 | whole window | `008da7fd` |
| Stop blocks: `check-verification-evidence` / `check-deep-review` / `check-version-tagged` | 52 / 11 / 3 | 35 / 10 / 2 | to 10-21 | |
| Broken hook runs ("No such file or directory") | 20 scripts, up to 158 each | 8 | 09-21 to 09-27, none after | |
| User corrections (narrow lexicon) | 2 | 2 | | |
| Runaway retries (3 or more messages, same failure) | 3 | 1 | | `846fe3ce` (`SubagentHandback` refused outside auto mode) |
| Skills with no invocation | `engine-bump`, `freeze`, `prep-tripo-prop`, `upstream-review`, `harness-retro` (new) | | | |

Context gathered by hand for the first rows: the docs-gap hook ran 12.5 s and 17.8 s on an idle
machine against its 10 s registration (two timed runs, 2026-10-21); it starts about four processes
for each of the 161 `Main/Features` folders. The quoting failures come from every agent type
(implementer 61, deep-reviewer 35, feature-builder 33, main session 18, Explore 16), mostly
`python - <<'EOF'` and `cat > file <<'EOF'` bodies containing an apostrophe, and `$'\r'` inside
`$(...)`. The encoding failures come from `deep-reviewer` (41) and the main session (16): the
console is cp1252, and `PYTHONUTF8=1` fixes the print (checked in this session, where one of this
retro's own scratch scripts hit the same error).

## Verdicts

Only changes a signal measures directly. The window holds 246 commits touching the harness paths;
the rest are this baseline's starting point, not judged.

| Change | Observed | Verdict |
|---|---|---|
| `7b342ed59` (09-27, #690): hook registrations anchored on the project dir | 20 scripts failed with "No such file or directory" in 8 sessions up to 09-27; none after | helped |
| `e046bdf09` (09-26, plan 027): the git gates run for the PowerShell tool | 5 PowerShell denies (subject version, push, doc drift, ModuleData, tracked files), all 10-03 to 10-17 | helped |
| `407570ece` (10-16): upstream check in its own hook, shared probe deadline in `session-start.sh` | 5 kills on 10-18, none 10-19 to 10-21; 10-18 was also the heaviest gate-kill day; five days old | too early (look again next retro) |
| `block-dangerous-git`, `block-broad-git-add`, `check-graphify-usage` | ask-only gates: an approved ask leaves no trace, so their fires cannot be counted | not measurable; keep |

## Proposals

Mike approved all five on 2026-10-21 ("fix it all"). The decision column says what was done.

| # | Proposal | Evidence | Form | Decision |
|---|---|---|---|---|
| P1 | Make `detect-docs-gaps.sh` finish inside its budget: kebab-case in bash without a process per folder, an inner time bound, and an UNCHECKED line on overrun | Killed at 39 of 39 starts that logged it; 12.5 s and 17.8 s measured idle | hook fix | done: one `sed` and one `tr` for all folders, 12.9 s to 0.5 s, output byte-identical to the old hook on a tree with 80 gaps; no inner bound needed at 0.5 s; `test_hooks.sh` 7l2 |
| P2 | Put the gate-kill numbers on #661 (skip the Python start-up when a call cannot concern a gate) and raise its priority | About 400 gate kills, each an ungated call, in load bursts; `block-dangerous-git` 88 | hook performance | numbers posted on #661. The prefilter #661 asks for is in place; the 445 kills are machine stalls (every gate on a killed call died together after 22 to 24 s, 212 of the calls held no git). Fix: the safety gates' registrations raised to 30 s in `settings.json` (applied by Mike, since the harness refuses an agent's edit to its own settings); inner bounds unchanged |
| P3 | Find exactly which Bash command shapes the tool breaks on, then turn the CLAUDE.md heredoc line into a gate or a failure hint for those shapes only | 151 failures in 51 session rows across every agent type, although CLAUDE.md already warns | gate or hint, after a test | done: the cause is length, not quotes (`bash -n` accepts 58 of 61; 140 of 142 at 8,000 or more failed, none under 7,000). New gate `check-bash-length.sh` refuses 7,500 or more with the fix in its reason; CLAUDE.md line corrected (the tool does halve `\\`, retested). Registered for PreToolUse `Bash` with P2's settings change |
| P4 | Tell `deep-reviewer` (no Write tool) how to run a multi-line check, in its own definition | 66 Write attempts, 35 quoting failures, 41 encoding failures | agent definition (loads on spawn only) | done: one bullet, a scratchpad helper built in parts under the length limit, and the halved `\\` |
| P5 | Set `PYTHONIOENCODING=utf-8` in `.claude/settings.json` `env` | 43 encoding failures; the env block reaches Bash; the flag fixes the print. `PYTHONUTF8=1` was tried first and rejected: it also makes `subprocess` decode child output as UTF-8, and `test_gen_troll_anim_clips` broke on PowerShell's cp1252 output (41 tests pass without it). Under `PYTHONIOENCODING=utf-8` the full tools suite passes (4,583 tests) | config | done: in `settings.json` `env` with P2's change |

Deferred, no proposal: the Edit "file has not been read yet" errors (135 in 20 sessions) come in
batches of parallel calls from one message, not retry loops, and the message already says what to
do; `check-verification-evidence` blocked 52 times, and whether that is a nag or a catch needs the
sessions read, which is the next retro's question.

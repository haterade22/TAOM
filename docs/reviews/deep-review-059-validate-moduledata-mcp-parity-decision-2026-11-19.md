# Deep review: the ModuleData commit hook's exit 2 note (plan 059, #623 decision A, 2026-11-19)

```
DEEP REVIEW REPORT
===================
Feature: plan 059 follow-up, check-moduledata-validation.sh answers a validator exit 2 with an
         UNCHECKED note (#623, the maintainer's decision A)
Date: 2026-11-19

Scope:   harness (one PreToolUse hook), its hook-suite section 7m, one feature doc line.
         Commit 09f03e8c7 only (diff 520c4de5e..09f03e8c7); the rest of the branch was reviewed
         already. The uncommitted plans/ edit is out of scope.
Blast radius: NOT IN SCOPE for graphify (no C# type changed). The hook's only caller is the
         harness (.claude/settings.json PreToolUse, Bash and PowerShell); the git pre-commit hook
         (.githooks/pre-commit run_gate) already reported exit 2 as "could not judge".
Waves:   one wave: Agent 5 (Data flow) and Tooling correctness.

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: NOT IN SCOPE (no engine code)
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     FAIL, 1 gap, 4 inconsistencies, 1 untested path (all LOW); all fixed
DESIGN:        SKIPPED BY TIER
XML:           NOT IN SCOPE (no ModuleData)
TOOLING:       FAIL, 4 findings (1 MEDIUM, 3 LOW); all fixed
```

## Details

### Agent 5, Data flow (10 flows)

| Lens item | Claim | Lead verdict | Evidence |
|---|---|---|---|
| 1 | Every exit 2 the hook can reach gets the note: `:944` (no ModuleData), `:985` (no install), an argparse usage error, a missing script; `:801` is unreachable | CONFIRMED as stated (no defect) | `validate_moduledata.py:936-937` routes `:801` only through `--regenerate-shipped-troop-ids`, which the hook never passes (hook `:125-140`). A real no-install run here: rc=2, 38 stderr lines. |
| 2 | The 10-line tail drops the no-install cause | CONFIRMED, LOW (F3) | Real stderr: the cause is line 6 of 38 (`WARNING: Bannerlord Modules folder not found`); the tail holds SKIPPED notices only. |
| 3 | JSON is safe for quotes, backslashes and non-ASCII; a byte that is not UTF-8 gives a silent `{}` | CONFIRMED, LOW (F2) | Sweep row `v2-badbytes`: `allow-silent` on 09f03e8c7. Row `v2-quotes`: valid on both. |
| 4 | Kit drift still blocks on a validator exit 2; untested | CONFIRMED as stated; test gap fixed | Hook `:163-165` sets RC=1 before the rc=2 branch; sweep rows `v2-* / k1-drift` deny on all three revisions. |
| 5 | The kit "could not judge" note is lost on exit 2 | CONFIRMED (F1, MEDIUM: see Tooling 1) | Sweep rows `v2-* / k-crash`: base `allow-note[K]`, 09f03e8c7 `allow-note[2]`. |
| 6 | The git pre-commit hook agrees | Accepted (no defect) | Read by the lens; not re-read by the lead. |
| 7 | `hooks-catalog.md:30` is stale | CONFIRMED, LOW (F5) | The row read "a validator crash is allowed with a note". |
| 8 | The branch does not change the time limit | CONFIRMED | The branch runs after the validator returned and adds one `cat` and one Python start. The 59 s worst case (45+2 validator, 10+2 kit) is older than this commit: `FOLLOW-UP` (2) below. |
| 9 | Laptop noise is acceptable | CONFIRMED | An allow with `additionalContext` asks nothing, and the note is true on a machine with no install. The maintainer chose it (decision A). |
| 10 | The exit 2 test greps text and never parses JSON | CONFIRMED, test gap fixed | 7m now parses the answer (`vm_says`) and round-trips a quote, a backslash and non-ASCII. |

### Tooling correctness

| Lens item | Claim | Lead verdict | Evidence |
|---|---|---|---|
| 1 | MEDIUM: exit 2 hides the career kit UNCHECKED note, a regression from the base | CONFIRMED (F1) | As Data flow 5. The rc=3 branch has the same gap and is older than this commit: `FOLLOW-UP` (1). |
| 2 | LOW: the `{}` fallback is silent | CONFIRMED (F2) | As Data flow 3. |
| 3 | LOW: the exit 2 test does not parse JSON, and two cases are missing | CONFIRMED, test gap fixed | Five 7m rows added or rewritten; three were red on 09f03e8c7 (below). |
| 4 | LOW: the catalog row is stale; the no-install wording is acceptable as is | CONFIRMED (F5); wording kept | The lens itself asked for no wording change. |

### Found by the lead

- **F4, LOW:** the new branch's comment named "a missing baseline tag" as an exit 2 cause. That
  `return 2` (`validate_moduledata.py:801`) is reachable only through
  `--regenerate-shipped-troop-ids`, which the hook never passes. Comment corrected.

### Probes the orchestrator asked for

- **Every exit 2 path gets the note:** `:944` (stub `v2-nomd`), `:985` (stubs `v2-noinstall` and
  `v2-real`, a byte replay of the real no-install stderr captured in this run), argparse (stub
  `v2-usage`), empty stderr (`v2-empty`). Each is `allow-note` with "exited 2" on the fixed hook.
  `:801` is unreachable from the hook.
- **Kit drift still blocks on exit 2:** deny on every `v2-* / k1-drift` row for the base, reviewed and
  fixed hooks, and pinned by a new 7m row.
- **JSON with quotes, backslashes, non-ASCII:** valid on both, and pinned by a new 7m row. Bytes that
  are not UTF-8: a silent `{}` on 09f03e8c7, a note on the fix.
- **Empty or missing ERR_FILE:** empty stderr gives the note with an empty tail (`v2-empty`). A missing
  file reaches the snippet as empty stdin (`cat` fails quietly), and `/dev/null` reads as empty. A
  probe hook whose snippet raises gave the fixed fallback note as valid JSON.
- **The 60 s registration:** the branch runs only after the validator returned. Slowest single stub
  run in the sweep: 9.35 s on the fixed hook (sandbox, stubs, cold Python starts). Not timed against
  a real no-install run with the hook's full code list.
- **Laptop noise:** acceptable. Every ModuleData commit on the laptop carries the note; it asks
  nothing and it is true.

### Differential sweep (gate changed)

88 cases: validator stubs for exit 0, exit 1 with and without findings, exit 2 (usage, no
ModuleData, no install, the real no-install stderr, quotes, bytes that are not UTF-8, empty) and
exit 3, crossed with kit stubs (clean, drift, crash, exit 2), under the Bash and PowerShell tool
names, against the base hook (520c4de5e), the reviewed hook (09f03e8c7) and the fixed one. Refused
(deny or ask) by an older revision and allowed by a newer one: **0**. Rows where the reviewed and
fixed hooks differ: 26, all exit 2 rows; in each, the note gains the kit sentence or the cause line,
or replaces a silent `{}`.

## Action items

1. F1 (MEDIUM): name the kit tool that could not judge in the exit 2 note. Fixed.
2. F2: decode stderr with replacement; a fixed fallback note instead of `{}`. Fixed.
3. F3: cause lines from above the tail go first. Fixed.
4. F4, F5: the comment and the catalog row. Fixed.
5. Test gaps: JSON parse, exit 2 with kit drift. Fixed.

## Improvements (Step 4)

```
APPLIED:     none as Step 4 improvements: the tier ran no Agent 3 or Agent 6, and every change
             was a defect fix, test first:
             .claude/hooks/check-moduledata-validation.sh, the exit 2 branch: kit sentence (F1),
               replacement decode and fixed fallback (F2), cause lines (F3), comment (F4).
               Tests in tools/test_hooks.sh 7m: "a validator exit 2 with stderr that is not
               UTF-8 still says UNCHECKED", "... from a missing install names the cause above
               the tail", "... with a kit tool that cannot judge names both". On 09f03e8c7 the
               7m section gave 12 passed, 3 failed (those three); on the fix, 15 passed, 0 failed.
               Characterisation rows green before and after: the JSON round-trip, and exit 2
               with kit drift denies.
             docs/reference/hooks-catalog.md:30 (F5).
NOT APPLIED: none.
FOLLOW-UP:   (1) The rc=3 branch (:171-184) and the rc=1 "no findings" note (:214-216) also end
               the hook before the kit note, and the rc=3 branch keeps the strict decode and the
               {} fallback (sweep rows v1-empty and v3 with k-crash: no kit sentence on any
               revision). No issue: the review lead cannot file issues; the orchestrator files
               one at wrap-up.
             (2) Worst case 45+2 s validator plus 10+2 s kit tool is 59 s against the 60 s
               registration, before git and Python start-up. Older than this commit. No issue:
               the orchestrator files one at wrap-up.
             (3) An unknown --code value is accepted by argparse and filtered to nothing, exit 0
               (validate_moduledata.py:912-914). No issue: CommitGateCoverageTests covers the
               hook's code list.
```

## Suite totals

Run in the worktree on the reviewed commit (base) and on the fix (final):

| Suite | Base (09f03e8c7) | Final |
|---|---|---|
| Hook suite (`CLAUDE_PROJECT_DIR` set to the worktree) | 1446 passed, 0 failed | 1451 passed, 0 failed (5 new 7m rows) |
| `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | Failed: 3, Passed: 43028, Skipped: 9, Total: 43040 | Failed: 3, Passed: 43028, Skipped: 9, Total: 43040 |
| `python -B -m unittest discover -s tools/tests -t .` | Ran 10561 tests, OK (skipped=13) | Ran 10561 tests, OK (skipped=13) |
| `python tools/lint_docs.py --fail-on-drift` | not run | exit 0 |

The three dotnet failures are the same on both runs and are localization tests this commit does not
touch (`EveryLanguage_DeclaresARowForEveryEnglishKey`, `NoTranslatedString_MixesWritingSystems`,
`EveryKey_IsSeededInAllTwelveLanguageFiles`).

VERDICT: READY FOR COMMIT

## Codex review

Codex not run: no paid dispatch was authorised for this item, so there are no Codex findings to
assess.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| (none) | not run | n/a | n/a | Codex not run |

- **Confirmed bugs:** none from Codex. **False positives:** none. **Design questions:** none.
- **Things Codex missed:** not applicable; the lens findings F1 to F5 are recorded above and in the
  RCA.

### Codex lessons (pending)

- Missed category for `.ai/review-reference.md` "Lessons From Prior Reviews": in a fail-open hook, a
  new branch that ends the hook early must carry every note the shared tail after it prints, and
  its own fallback must never be `{}` (F1, F2). Codex did not run here, so this is a category for
  its prompt, not a recorded Codex miss.

## Records

- RCA: `docs/reviews/rca-validate-moduledata-mcp-parity-decision-2026-11-19.md`
- Lessons: two entries appended to `docs/reviews/lessons/build-tooling-workflow.md`
- Review log: one entry appended to `docs/reviews/REVIEW-LOG.md`

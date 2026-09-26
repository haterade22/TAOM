# Deep review: plan 027, PowerShell gate coverage (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 027, the nine PreToolUse git gates cover the PowerShell tool, read PowerShell
         syntax, and close validate-push's remaining bypass shapes
         (branch improve/027-powershell-gate-coverage, 96afb6fb..05dbc0d4)
Date:    2026-09-24 (review lead run 2026-09-26)

Scope:   harness (9 gates, _pybin.sh, the new _shellwords.py reader, mark-verification-run.sh,
         tools/test_hooks.sh, tools/tests/test_shellwords.py), docs. No C#, no XML.
Waves:   one wave: Standards (1), Efficiency (3), Completeness (4), Data flow (5), Design (6),
         Tooling correctness; Codex adversarial (gpt-6-astra, ultra) in parallel.

STANDARDS:     FAIL: 5 LOW violations, 1 process item (no issue)
COMPATIBILITY: NOT IN SCOPE (no engine code)
EFFICIENCY:    FAIL: 4 issues (0 high, 1 medium, 3 low)
COMPLETENESS:  INCOMPLETE: no GitHub issue; fallback untested; option-value class half closed;
               four doc statements wrong; missing reader pins
DATA FLOW:     FAIL: 5 gaps (1 regression rated MEDIUM by the lens, HIGH here), 4 inconsistencies,
               1 timing note
DESIGN:        6 KEEP proposals (5 apply, 1 follow-up)
XML:           NOT IN SCOPE
TOOLING:       FAIL: 13 findings (3 HIGH, 1 MEDIUM, 9 LOW)
```

## Details

Every finding below was re-checked by the review lead against the worktree before any change. The
lead reproduced each validate-push claim by running the hook at `96afb6fb` (`git show`, scratch
copy), at `05dbc0d4` and after the fixes, on a trunk-branch and a feature-branch scratch repo, under
both tool names. PowerShell grammar claims were checked with PowerShell 7's
`[System.Management.Automation.Language.Parser]::ParseInput`. Scratch evidence:
`E:\repos\taom-improve\scratch\review-027\lead\` (`probe.py`, `cases1.txt`, `fuzz_vp.py`,
`fuzz1.txt`, `timing.py`, `parse.ps1`).

### Agent 1: Standards

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| V1 | LOW | `harness-facts.md` "Last verified" not moved for two 2026-09-25 facts | CONFIRMED | Fixed: 2026-09-25 |
| V2 | LOW | `harness-facts.md` crossed the 12,288 B path-rule cap; `hook-authoring.md` grew past it | CONFIRMED (report-only size-warn) | Partly: the hook-authoring table is gone (V3) and the harness-facts grep recipe trimmed; the rest is FOLLOW-UP |
| V3 | LOW | `hook-authoring.md` table restated the catalog and claimed 7e rows "under both tool names" that do not exist; "never from `tool_input.command` directly" contradicted validate-push | CONFIRMED | Fixed: pointer to the catalog and the unit tests; "alone" |
| V4 | LOW | Catalog lines 6, 20, 25 stale (jq first, "Bash-matched", `*git*`) | CONFIRMED | Fixed |
| V5 | LOW | BASH_GIT's over-reach into heredoc text recorded only in the plan | CONFIRMED (probe: `Git reset --hard is gated` in a heredoc reads as `git reset ...`) | Fixed: one catalog sentence, a known over-block |
| V6 | PROCESS | No GitHub issue for decision 61 | CONFIRMED | NEEDS MIKE (orchestrator; `/issue` is never auto-invoked) |
| ptr | n/a | `HEAD^{tree}` read as `HEAD^` then a block | FALSE POSITIVE: PowerShell's own parser does the same (plan table; `test_braces_inside_a_word_split_it`) | None |

### Agent 3: Efficiency

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| 1 | MED | validate-push: four Python starts and a doubled bash split; 1 MB PowerShell near the 5 s kill | CONFIRMED: 05dbc0d4 median 4,466 ms vs base 3,485 ms (5 runs) | Fixed as Fix B in spirit: one `_shellwords.py push` run returns every split. Fixed: median 746 ms; plain Bash push 345 ms (05dbc0d4 783, base 434) |
| 2 | LOW | broad-add fork-free strip quadratic on one long segment | CONFIRMED: 100 KB message 2,906 ms (base 866) | Fixed: `sed` above 4 KB, 686 ms; verdicts identical (same output as the loop) |
| 3 | LOW | `taom_hook_command` extra fork, about 20 ms | CONFIRMED | NOT APPLIED: simplicity criterion, a 20 ms parallel win for a nine-call-site signature change |
| 4 | LOW | `_escape` unbounded `}` search | CONFIRMED | Fixed: bound at 6 hex digits (PowerShell's limit); unit test |

### Agent 4: Completeness

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| F1 | MED | No issue; five owed live PowerShell checks have no home | CONFIRMED | NEEDS MIKE (orchestrator files it; the five checks become its checklist) |
| F2 | MED | `taom_hook_command` fallback untested; "says so" overclaims (stderr of an exit-0 gate reaches no one) | CONFIRMED | Fixed: 7e fallback rows (block-dangerous-git asks and prints the note; validate-push still refuses a trunk force push); comment and catalog reworded |
| F3 | MED | `--recurse-submodules` value and `-fo ci.skip` taken for the remote | CONFIRMED (pre-existing at base, incomplete fix) | Fixed with Agent 6 proposal 1; 7c rows |
| F5 | LOW | Four doc statements wrong | CONFIRMED | Fixed (hook-authoring, catalog 25 and 56, mcp-servers 14) |
| F6 | LOW | No machine check that a gate reads through `taom_hook_command posix` | CONFIRMED | Fixed: 7e grep row per listed gate. Stocktake heading: FOLLOW-UP |
| F7 | LOW | Missing reader pins (`u{}`, `@(`, misplaced `'@`, empty argument, bad payloads) | CONFIRMED (pins, not bugs) | Fixed: unit tests added |

### Agent 5: Data flow

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| T8 | MED (lead: HIGH) | Two shapes refused at base pass: PowerShell `git -C "E:\R&D" push --force origin ("<trunk>")`; Bash heredoc holding one `"` then `git -c "user.name=a #b" push --force origin <trunk>` | CONFIRMED (base rc 2, 05dbc0d4 rc 0) | Fixed: raw command's own quoted split judged again; comment dropped only where no quote precedes it; 7c rows |
| T9 | LOW | `-fo ci.skip`, `--push-opt ci.skip` on a trunk pass | CONFIRMED (also at base) | Fixed; 7c rows |
| T10 | LOW | Confirm gates do not split at a lone `&` | CONFIRMED, pre-existing split line | FOLLOW-UP (with Agent 6 proposal 6) |
| T11 | LOW | PowerShell `. git commit ...` skips the label gate | CONFIRMED (ParseInput: `CMD(Dot)`) | Fixed; unit and 7e rows |
| T12 | LOW | `GIT_TRACE=0 GIT commit/reset` not renamed | CONFIRMED | Fixed; unit, section 6 and 7e rows |
| T13 | LOW | `pbpaste \| git commit -F -` denied (base allowed) | CONFIRMED | Fixed; section 6 rows |
| T14 | LOW | PowerShell `-m $(Get-Content f -Raw)` allowed while the Bash twin is denied | FALSE POSITIVE: the content is unknowable, and allow is the gate's stance for unknown text; the Bash deny is the existing over-block on the literal `$(cat f)` subject | None |
| T15 | LOW | 7c comments still say the hook reads `tool_name` | CONFIRMED | Fixed |
| T16 | LOW | `refs/heads/*` reported as "master" | CONFIRMED | Fixed: trunks first in `PROTECTED`, so the message names `bannerlord-1.5.x` |
| T18 | LOW | 1 MB timing | CONFIRMED | Fixed with Efficiency 1 |

### Agent 6: Design and elegance

| # | Proposal | Verdict | Action |
|---|---|---|---|
| 1 | Short-option clusters and `--recurse-submodules` | KEEP, CHANGING | APPLIED as the fix for confirmed defects (T9, Tooling F2, Completeness F3), in a more conservative form: an `f` anywhere in a cluster still forces, so `-of origin` stays refused as at base instead of becoming allowed |
| 2 | `${name}` one word | KEEP, CHANGING | APPLIED as a defect fix: `git -C ${env:USERPROFILE}\repo commit -m "no label"` was allowed, `... reset --hard` got no ask |
| 3 | jq fallback into `taom_hook_command` | KEEP, PRESERVING | APPLIED: three gates lose their if/else and validate-push's fallback uses the helper; the Python path is unchanged (7e), the jq path reads the same `jq -r` as before |
| 4 | `quote()` double-quotes a word holding an apostrophe | KEEP, CHANGING | NOT APPLIED: needs Mike (removes a false ask on `git commit -m "don't use -a here"` from PowerShell; the ask errs safe) |
| 5 | One `pre_payload` builder in the harness | KEEP, PRESERVING | APPLIED: four copies to one helper |
| 6 | Confirm gates split at a lone `&` | FOLLOW-UP | Listed, not applied (pre-existing line) |

### Tooling correctness

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| F1 | HIGH | Quoted ` #` hides a push: PowerShell typographic quotes (`if (‘a #' -ne 'x’) {...}`, `-o ‘ci #1’`), Bash heredoc `Don't stop` then `-o 'ci #1'`, `echo $'it\'s'` then the same | CONFIRMED, all four base rc 2, 05dbc0d4 rc 0 | Fixed: quote-aware comment drop in the blind split, typographic quotes in the reader; 7c rows (typographic ones sent as literal characters; their JSON `\u2018` escape twins were added at convergence) |
| F2 | HIGH | `git push -fo ci.skip origin` on a trunk passes | CONFIRMED (also at base) | Fixed |
| F4 | HIGH | PowerShell assignment hides git from three gates | CONFIRMED (ParseInput runs the command) | Fixed: `$x = `, `$x=`, `[type]$x +=` end the statement head |
| F3 | MED | `piped_text` false denies | CONFIRMED | Fixed (see Codex 2) |
| F5 | LOW | broad-add quadratic | CONFIRMED | Fixed (Efficiency 2) |
| F6 | LOW | Reader diverges: `'a'b` is two words, a pipe ends `--%`, no-break space separates | CONFIRMED (ParseInput) | Fixed; unit tests |
| F7 | LOW | `MSYS_NO_PATHCONV` breaks `TAOM_HOOKS_DIR` | UNVERIFIED exposure (variables unset here) | FOLLOW-UP |
| F8 | LOW | Fallback untested | CONFIRMED | Fixed (Completeness F2) |
| F9 | LOW | Three Python starts in validate-push | CONFIRMED | Fixed (Efficiency 1) |
| F10 | LOW | Lone `&` in confirm gates | CONFIRMED, pre-existing | FOLLOW-UP |
| F11 | LOW | Heredoc `Git reset --hard` now asks | CONFIRMED, documented over-block | Documented (V5) |
| F12 | LOW | `'git' reset --hard` and `X=1 GIT reset` unconfirmed | CONFIRMED | Fixed |
| F13 | LOW | No rows for `--repo`, `--receive-pack`, `--exec` | CONFIRMED | Fixed: 7c rows |

## Action items (all done on the branch unless marked)

1. validate-push regressions (T8, F1, Codex 1): fixed; differential sweep clean.
2. Option-value grammar (F2, T9, F3): fixed.
3. PowerShell statement heads (F4, T11, `${}`, F6, typographic quotes): fixed.
4. Commit gate piped producers (Codex 2, F3, T13): fixed.
5. Timing (Efficiency 1, 2, 4): fixed and measured.
6. Tests and oracles (Codex 4, F8, Completeness F6, F7, F13): fixed.
7. Docs (V1 to V5, Completeness F5, T15, T16): fixed.
8. GitHub issue with the five owed live PowerShell checks: orchestrator (NEEDS MIKE).

### How the regressions were closed, and proven

`validate-push.sh` now reads every candidate line from one `_shellwords.py push` run: the POSIX
text and the raw command cut quote-blind (a `#` comment dropped only where no ASCII or typographic
quote comes before it; since the convergence pass, anywhere before it in the command), the POSIX text split outside quotes plus each such segment
with argument boundaries kept (`-o "ci skip"`, `-o ""`), and the raw command split outside quotes
with the tool's own escape (the pre-027 split). `judge_command` judges the positionals with and
without an option-value skip and blocks if either does. Without Python or when the reader fails, it
judges the raw command as before plan 027, comments included.

A differential sweep (`fuzz_vp.py`) fed the `96afb6fb` hook and the fixed hook 5,896 payloads (22
push bodies by 11 prefixes by 6 suffixes, plus `bash -c` wraps, each under both tool names on a
trunk and a feature branch): **0 refused at base and allowed now**, 190 newly refused, the rest
equal. That corpus lacked one class, a quoted value the blind split cuts inside: the convergence
pass found it and it is fixed (see Convergence). The one deliberate relaxation, a trunk named only in a trailing comment, is not in that
corpus; it stays allowed when no quote comes before the `#`, and a quoted refspec before it
(`git push --force origin "feature" # bannerlord-1.5.x later`) is refused again, the safe side.

Behaviour changes to note: a PowerShell string or variable that opens a statement now reads as
`echo <value>` (PowerShell prints it; `'git' commit ...` is a parse error there and runs nothing),
so such a line no longer reaches the commit gate; and `git push -of origin` stays refused although
git reads it as `-o f`.

## Improvements (Step 4)

APPLIED:
- `.claude/hooks/validate-push.sh` and `_shellwords.py push_lines`: one reader run for every split
  (Efficiency 1, Tooling F9); proven by all 7c rows and the timing above.
- `.claude/hooks/block-broad-git-add.sh:100-115`: `sed` above 4 KB (Efficiency 2); same output as
  the loop, 7e broad-add rows green.
- `.claude/hooks/_shellwords.py` `_escape`: bounded `}` search (Efficiency 4);
  `test_unicode_escape_takes_one_to_six_hex_digits`.
- `validate-push.sh` short clusters and `--recurse-submodules` (Design 1, as a defect fix); 7c rows.
- `_shellwords.py` `${name}` (Design 2, as a defect fix); unit and 7e rows.
- `_pybin.sh taom_hook_command` jq fallback, four gates simplified (Design 3); 7e rows and the
  fallback rows green.
- `tools/test_hooks.sh` `pre_payload` (Design 5); pass count only grew.

NOT APPLIED:
- `_pybin.sh:175` helper sets `COMMAND` itself (Efficiency 3): simplicity criterion, tiny win,
  nine call sites and two docs churn.
- `_shellwords.py quote()` apostrophe in double quotes (Design 4): behaviour-changing, needs Mike.

FOLLOW-UP (pre-existing code, not applied; no issue filed here because `/issue` is never
auto-invoked, the orchestrator decides):
- Confirm gates split at a lone `&` (`block-dangerous-git.sh:70`, `block-broad-git-add.sh:80`;
  Data flow T10, Tooling F10, Design 6).
- `MSYS_NO_PATHCONV` and `TAOM_HOOKS_DIR` (Tooling F7), exposure unverified.
- `.claude/skills/skill-stocktake/SKILL.md:51` heading "PreToolUse(Bash) hooks specifically".
- `docs/reference/rules-catalog.md:40` omits the both-shells convention.
- `docs/reference/mcp-servers.md:47-48`: "Nothing matches `mcp__*`" and the retired
  CHANGELOG-staged gate.
- `hooks-catalog.md:3` says the harness counts both registration surfaces; section 2 prints 29.
- `harness-facts.md` still over the 12,288 B path-rule cap (size-warn).
- validate-push: brace expansion `{<trunk>,x}`, abbreviated `--force-w` and `--mirr`; the label
  gate allows `time git commit ...` and `command git commit ...` (all at base too).
- `docs/reviews/LESSONS-LEARNED.md` category count (217) lags the file (223 headings); counts are
  reconciled at merge.

## Codex review

Complete (`docs/reviews/raw/codex-adversarial-027-powershell-gate-coverage-2026-09-24.md`, ends
"END OF CODEX REVIEW"); gpt-6-astra, reasoning ultra, 239,839 tokens. Static traces only, no
execution. It cross-referenced every registration, timeout, mode, protected name and marker path,
and quoted git's push-option parser.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P1 | HIGH | Yes | `git push --force -o "" origin HEAD:<trunk>`: base rc 2, 05dbc0d4 rc 0, reproduced. The multiword half (`-o "ci variable" origin` on a trunk) passed at base too. Both fixed |
| 2 | P2 | MEDIUM | Yes | `printf 'docs: v2.0.30 - %s\n' x \| git commit -F -`, `./message-generator \| ...` denied on invented text; base allowed. Fixed |
| 3 | P2 | LOW | Partly | `'.\build.ps1'` alone marked: reproduced reasoning, fixed by the reader's `echo <value>`. `$verify = { dotnet test }` still marks: the reader must read a block's body as commands for the git gates; known limitation, NEEDS MIKE |
| 4 | P3 | LOW | Yes | 7e's inventory came from the settings under test. Fixed: the nine names are listed, an unlisted shell gate fails |

- **Confirmed bugs:** 1, 2, 3 (literal part), 4.
- **False positives:** none.
- **Design questions:** 3 (script-block assignment marking).
- **Things Codex missed:** every other validate-push regression (quoted ` #` after a heredoc
  quote, typographic quotes, a separator inside a quoted path with a parenthesised refspec), the
  PowerShell assignment, `.` and `${}` forms, the option-cluster grammar, and all timing findings.
  Its known-suspect table rated suspect 10 (dead gate) disputed, correctly.

Phase 3e root-cause table: in `docs/reviews/rca-powershell-gate-coverage-2026-09-24.md`
(findings 3, 7, 13, 14 are Codex's).

## AGENTS.md lessons (pending)

- Bugs Codex typically misses: quote parity carried across heredoc lines into a later comment
  strip or skip, and a target shell's statement heads (PowerShell assignment, `.`, `${}`,
  typographic quotes) when a reader translates between shells. Ask for a base-versus-new
  differential run on any gate change that relaxes a shape.
- What Codex does well: boundary loss on quote-flattened tokens (the empty `-o ""` value), and
  modelling what a piped producer really prints before a gate reads it as data.

## Settings changes for the orchestrator

None. The single `Bash|PowerShell` PreToolUse group from Step 0 is unchanged and still correct.

## NEEDS MIKE

1. File the GitHub issue for decision 61 with the five owed live PowerShell checks as its
   checklist (Standards V6, Completeness F1).
2. `$v = { dotnet test }` in PowerShell marks verification (Codex 3): accept as a known limitation,
   or give the mark hook its own reading that skips assigned script blocks.
3. Design 4: stop the false ask on a PowerShell commit message holding an apostrophe and ` -a`.

## Verification

Final runs, in the worktree after every fix:
- `python -m unittest tools.tests.test_shellwords`: Ran 53 tests, OK (33 before the review).
- `bash tools/test_hooks.sh`: 773 passed, 0 failed (705 before the review). The first run printed
  771 passed, 2 failed, both timing rows of hooks this change does not touch (`session-start.sh`
  7,266 ms and `pre-compact.sh` 3,331 ms against 3,000 ms, machine load); the rerun passed.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: Passed 10767, Failed 0,
  Skipped 2 (the branch contains a39a9c86, so no failure was allowed).
- `python -m unittest discover -s tools/tests -t .`: 2179 run, 2 failures, both outside this
  change and on live data (`test_clan_heraldry_specs`, `test_generate_career_kits`), the baseline
  pair Completeness reported.
- `python tools/lint_docs.py --dash-base 96afb6fb`: 0 dashes, 0 dead links; `--fail-on-drift`
  exit 0.

VERDICT: READY FOR COMMIT (Step 4 complete; the convergence pass is owed to the orchestrator,
since this lead cannot spawn a reviewer).

## Convergence

A convergence reviewer read the staged review fixes (`git diff --cached 05dbc0d4`) and ran the
base (`96afb6fb`), pre-fix (`05dbc0d4`) and fixed hooks side by side: hook suite 773 passed,
reader tests 53 OK, gate inventory and parity checks clean, and four defects. The review lead's
second pass re-checked each against the code before changing anything; all four were confirmed,
none was a false positive.

| # | Sev | Defect | Verified | Fix |
|---|---|---|---|---|
| C1 | HIGH | `validate-push.sh` dropped a push when the quote-blind split cut inside a quoted value holding `;`, `\|`, `&` or a newline before a `#` (`X="a;b #c" git push --force origin bannerlord-1.5.x`, `X='a;b #c'`, `env "X=a;b #c"`, `X="l1<newline>#2"`, `X="a\|#c"`, `X="a&#c"`) after an earlier line left a quote open | CONFIRMED: of the 18 prefix and value shapes rerun, all 18 rc 2 at base, 7 rc 0 on the staged hook (the others were caught by another split) | `_shellwords.py _blind_pieces`: a `#` comment is dropped only when no quote (ASCII, typographic or backtick) occurs anywhere earlier in the whole text, not just in its piece. Unit test `test_blind_split_inside_a_quoted_value_keeps_the_push` (18 subtests, red first), `test_comment_after_any_quote_is_judged`; eight 7c rows |
| C2 | LOW | `_words_kept` ran `shlex.split` (quadratic in a word's length) on every quoted segment holding `push` | CONFIRMED by timing | Only segments up to 4 KB (`WORDS_KEPT_MAX`) are re-split; the shapes it exists for are short, and without it the positionals are still judged unskipped. Unit test `test_argument_boundaries_skip_a_long_segment`; a `push-big` 7e timing payload for all nine gates |
| C3 | LOW | `$null =git`, `[int] $x = git`, `$a.b=git`, `$a[0]=git`, `$x, $y = git` hid git from the gates | CONFIRMED: `ParseInput` (PowerShell 7.6.6) returns an `AssignmentStatementAst` for each, left side Variable, Convert, Member, Index and ArrayLiteral; the staged hooks allowed all six new 7e rows | `_assignment_head` reads the left side as ParseInput does (variable, cast glued or spaced, member, index, comma list, an operator glued to its right side). Unit tests for each plus the non-assignments `$x -eq 1`, `$x \| git push`, `[int] 5`, `[Console]::WriteLine('x')`; six 7e rows (commit gate and `block-dangerous-git`) |
| C4 | LOW | A 7c comment said the typographic rows were sent as JSON escapes; the bytes were literal U+2018 and U+2019 | CONFIRMED | Both spellings are now sent (literal and `\u2018`/`\u2019` escape text), comment reworded; F1's row above corrected |

**Statements corrected.** The "never hides" claims in `validate-push.sh`, `hooks-catalog.md`
(`validate-push.sh` row), this report's regression summary, and the RCA summary; the RCA gains
rows 16 to 19.

**Differential sweep after the fixes** (`validate-push.sh` at `96afb6fb` against the working tree,
a feature-branch scratch repo, 17 push bodies by 9 prefixes by 4 suffixes = 612 shapes per tool
name, the reviewer's classes included): under Bash and under PowerShell alike, 8 refused at base
and allowed now, all 8 the named relaxation (`git push --force origin feature # bannerlord-1.5.x
later`, alone or after `cd /x`, with any suffix); 1 newly refused; no other exit code.

**Timing** (`git commit -m "<N KB of 'push the thing '>" && git push --force origin
bannerlord-1.5.x`, Bash tool, median of 3):

| Size | base | staged | fixed |
|---|---|---|---|
| 300 KB | 2,933 ms | 4,406 ms | 3,966 ms |
| 400 KB | 3,555 ms | 6,082 ms | 5,233 ms |

The reader itself now takes 59 ms on the 400 KB payload. The rest is bash judging the 400 KB
`git commit -m "push ..."` line as a push with about 27,000 positionals (4,072 ms at base, 5,189 ms
fixed on that line alone): `judge_command`'s longer per-token loop from the review fixes (two
positional lists, cluster and prefix handling). So a window remains, roughly 350 to 450 KB of one
quoted segment holding the word `push`, where the base gate finished inside its 5 s registration
and the fixed gate does not (a killed gate fails open). Base itself fails from about 450 KB. Not
fixed in the convergence pass (no new design work there); FOLLOW-UP for the orchestrator.

**Follow-up, closed by the orchestrator.** `_shellwords.py push` now returns its lines shortest
first. `validate-push.sh` stops at the first refused line, so the short force push is judged
before the long message whose every word would be read as a refspec. The verdict is unchanged
(any line blocks); only the stop comes earlier. Unit test `test_shortest_line_first`, red first
(the 6 KB commit line came first). Same payload, median of 3, rc 2 in every run:

| Size | base `96afb6fb` | `747b6dae` | sorted |
|---|---|---|---|
| 300 KB | 2,737 ms | 4,012 ms | 295 ms |
| 400 KB | 3,588 ms | 5,178 ms | 294 ms |
| 800 KB | 7,264 ms | 11,361 ms | 358 ms |

PowerShell gives the same picture (423 ms sorted at 800 KB). One long segment that holds both
the `push` word and the real push (`git -c x="<long text holding push>" push --force ...`) still
walks every word; base did the same, so that is no regression.

**Final runs** (worktree, after every fix):
- `python -B -m unittest tools.tests.test_shellwords`: Ran 58 tests, OK (53 before).
- `bash tools/test_hooks.sh`: 798 passed, 0 failed (773 before; 8 7c rows, 2 typographic escape
  rows, 6 7e rows, 9 `push-big` timing rows added). `validate-push.sh` took 1,554 ms of 5 s on
  `push-big` (105 KB).
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: Passed 10767, Failed 0,
  Skipped 2 (the branch contains a39a9c86, so no failure was allowed).

CONVERGENCE VERDICT: 4 of 4 defects fixed; one timing window recorded as FOLLOW-UP.

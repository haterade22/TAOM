# Deep review: plan 013, Bash hook prefilter (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 013, let non-git Bash calls skip the Python start-up in every Bash hook
         branch improve/013-bash-hook-prefilter, diff 7f02fc8d..90141357
Date:    2026-09-24

Scope:   scripts (13 hooks, tools/test_hooks.sh), harness docs (hook-authoring.md,
         hooks-catalog.md), CHANGELOG. No C#, C++, XML or XSLT.
Waves:   Standards, Data flow, Tooling correctness, Efficiency, Completeness, Design
         (lenses run by the orchestrator); Codex adversarial (gpt-6-astra, ultra).

STANDARDS:     FAIL, 4 violations (1 MED, 3 LOW) + 2 NITs
COMPATIBILITY: NOT IN SCOPE (no engine code)
EFFICIENCY:    PASS, 0 defects; 3 proposals (1 APPLY-able but behaviour-changing, 2 follow-up)
COMPLETENESS:  INCOMPLETE: no GitHub issue; 4c flake; section 4 contract no longer past
               the prefilter; suggest-compact trigger rows; premise wording; parity evidence
DATA FLOW:     PASS for every hook (13 prefilters connected); 2 gaps and 5 inconsistencies
               in the test and docs
DESIGN:        3 KEEP proposals (3 APPLY, 0 FOLLOW-UP)
XML:           NOT IN SCOPE
TOOLING:       FAIL, 3 findings in changed code (1 MED, 2 LOW) + 3 follow-ups
```

**Verdict on the change itself:** every one of the 13 prefilters accepts everything its hook's
own trigger needs. Four independent checks agree: the Data flow traces, the Tooling lens's
300-case old-versus-new parity run (stdout, stderr, exit code and marker file compared, 0
differences), the Standards lens's rerun of the plan's own parity script pinned to `7f02fc8d`
(`parity: 156 cases, 0 differences`, read in the lens's `parity-out.txt`), and Codex's source
trace. Every defect sat in the new test section, the premise wording and one catalog paragraph.

## Findings, verified against the worktree

| # | Sev | Finding | Raised by | Verdict | Evidence I read or ran |
|---|---|---|---|---|---|
| F1 | MED | `tools/test_hooks.sh` 4c counted starts of a fake interpreter pinned through `TAOM_PYBIN`. `_pybin.sh:89` probes the pin under `timeout -k 0.2 0.8`, and `:98-114` silently falls back to the real `python` when the pin misses it, so a loaded machine reads 0 starts: a false "prefilter is narrower" failure, or a false pass on a no-interpreter row. The comment claimed "deterministic on any platform". The builder's own `scratch/013/green.txt:193` recorded the false failure, a plan STOP condition that the commit does not mention | Standards 1, Data flow 5, Tooling 1, Completeness M1 (Codex left it UNVERIFIED) | CONFIRMED, fixed | Reproduced: a fake that sleeps 1 s made `check-changelog-changed.sh` resolve `/c/Python314/python` on `cd /x\ngit commit -m x` with 0 counted starts, while the `bash -x` trace showed 1 `source` of `_pybin.sh`. `green.txt:193` read |
| F2 | MED | None of section 4's five payloads holds `git`, `dotnet` or `build.ps1`, so the exit-code, JSON and 3 s contract stopped at every Bash hook's prefilter; 4b and 4c discard exit code and output. Nine hooks had no committed check on their parse path | Data flow 8, Completeness M2, Design P1, Efficiency cross-lens | CONFIRMED, fixed | Mutation `exit 3` after the `source` in `notify-test-results.sh`: the committed suite passed 310/0 (`red-old-test-mutated.txt`); the fixed suite fails `notify-test-results.sh [bash-trigger] exit 3` |
| F3 | LOW | 4c gave `suggest-compact.sh` only a `git commit` row, so deleting its `*dotnet*` and `*build.ps1*` arms stayed green; "still parses" claimed more than `n >= 1` proves (the probe alone satisfies it) | Data flow 6, Tooling 2a and 2b, Completeness L1, Codex 1 | CONFIRMED, fixed | Mutation removing both arms: the committed suite passed; the fixed suite fails both new rows. Row text now says "reaches `_pybin.sh`" |
| F4 | LOW | 4c discovery took only `''` or an exact `Bash` alternative and skipped `PostToolUseFailure`; a Bash hook under `*`, `Bash.*` or on PostToolUseFailure would escape it. Nothing escapes today | Data flow 7, Tooling 2c | CONFIRMED, fixed | `settings.json` matchers read (`''`, `Bash`, `Edit\|Write`, `mcp__.*`); discovery now uses `re.fullmatch` with `''` and `*` as every tool, and still finds the same 13 rows |
| F5 | LOW | Hook comments and the CHANGELOG said "JSON never escapes an ASCII letter". JSON allows `\u0067`; the premise is how Claude Code writes the payload, and the durable docs did not name its re-check | Standards 2, Data flow 9, Completeness L2, Codex 2 | CONFIRMED, fixed | Wording in 12 hooks now says "Claude Code never escapes an ASCII letter"; the CHANGELOG and `hooks-catalog.md` state the premise, its evidence (#647 raw UTF-8) and the re-check after an upgrade |
| F6 | LOW | `hooks-catalog.md`: the new paragraph sat between the `_pybin.sh` paragraph and "This paragraph used to say ...", breaking that back-reference; "about 200 ms" disagreed with the measured 256 to 451 ms; "`*dotnet*` and `*build.ps1*`" read as both required | Standards 3, Data flow 10, Tooling 3, Completeness NITs | CONFIRMED, fixed | Paragraph moved below the history paragraph, figure and wording corrected; CHANGELOG hook list made exact |
| F7 | LOW | Plan 013 has no GitHub issue; the live proof owed after merge has no tracker | Standards 4, Completeness | NEEDS MIKE | Filing an issue is public and `/issue` is never auto-invoked |
| F8 | LOW | The "156 payload cases, no changed decision" claim had no saved output, and `parity.sh` read the old side from `HEAD` | Data flow 11, Completeness L3, Codex note | FALSE POSITIVE as a defect | The claim holds: the Standards lens reran it pinned to `7f02fc8d` (`parity: 156 cases, 0 differences`), and the Tooling lens's wider 300-case run found 0 differences. Only the evidence trail was missing; this report is it |
| N1 | NIT | `hook-authoring.md:153` ran to 116 characters; the 4c comment quoted the planning range "200 to 330 ms" | Standards NITs | CONFIRMED, fixed | Rewrapped (blob still 12,212 B, under the 12,288 B cap); comment now says 256 to 451 ms |

**Merge note (not a defect of this branch):** `git merge-tree` against `improve/008` and
`improve/010` conflicts in `notify-test-results.sh` and `CHANGELOG.md` (Data flow 12,
Completeness). Keep 013's order (read input, prefilter, `source`) with 008's header comment, then
run `bash tools/test_hooks.sh` and check 4c and 7c.

```
─────────────────────────
ACTION ITEMS
─────────────────────────
1. F1 4c oracle: fixed (bash -x trace of the _pybin.sh source; fake kept only as a cheap parse
   and a second witness on the negative row; timing-dependent premise check removed).
2. F2 section 4: fixed (bash-trigger payload "git status && dotnet --info").
3. F3, F4 4c coverage and discovery: fixed.
4. F5, F6, N1 wording and catalog: fixed.
5. F7 GitHub issue for plan 013 and its owed live proof: Mike.
6. Live proof after merge (plan 013:695): a two-line cd then git commit --dry-run -m "no label
   here" must be denied; a plain ls must pass with no hook message.
7. Step 4.6 convergence pass: not run (this delegate cannot spawn agents); the orchestrator owes
   one deep-reviewer pass over the fix diff, standards and behaviour parity only.

─────────────────────────
IMPROVEMENTS (Step 4)
─────────────────────────
APPLIED:
- tools/test_hooks.sh section 4 PAYLOADS: bash-trigger payload (Design P1). Proof: the exit 3
  mutation above, red on the committed suite, green-detected on the new one.
- tools/test_hooks.sh 4c pf_run: xtrace oracle (Tooling 1). Proof: with a fake that sleeps 1 s
  the new suite ran 338 passed, 3 failed, the 3 being exactly the two planted mutations; no git
  row false-failed (green-new-test-mutated-slowfake.txt).
- tools/test_hooks.sh 4c: suggest-compact dotnet and build.ps1 rows, "reaches _pybin.sh" naming,
  regex matcher discovery with PostToolUseFailure (Tooling 2a, 2b, 2c).
- docs/reference/hooks-catalog.md paragraph placement and wording (Tooling 3).
NOT APPLIED:
- Design P2 / Efficiency F1, six commit gates and suggest-compact prefilter on `commit` rather
  than `git`: needs Mike. Behaviour-changing (the no-Python stderr warning stops on non-commit git
  calls), needs a section 5 payload change, and replaces the plan's chosen literal.
- Design P3, validate-push on `push`, block-no-verify on `no-verify`: needs Mike, same reasons.
- Efficiency F3, suggest-compact throttle before the parse: rejected under the simplicity
  criterion. It saves two Python starts only in the ten calls after a boundary hint, and it would
  make 4c's positive rows depend on throttle state. Design lens also judged it too small.
- harness-facts.md row for the serializer premise (Standards 2, Data flow 9): held back because
  plan 013 kept that file out of scope while another session edits it; the catalog carries the
  premise and its re-check until then.
FOLLOW-UP (pre-existing code; no issue filed, filing is Mike's call):
- HIGH: validate-push.sh:54 reads only the first line (`read -r -a TOKENS <<< "$CLEAN"`), so a
  force push after a `cd` line passes. Verified: single-line rc=2, two-line rc=0. Known to the
  plan (plans/013:700). Recorded as Deferred on the follow-up commit.
- MED: mark-verification-run.sh:65-70 strips `dotnet` as an env prefix from the canonical
  `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`, so the repo's own test command
  never marks. Verified: not-marked. It also splits inside quotes (Tooling F1).
- suggest-compact.sh and notify-test-results.sh print only to stderr on exit 0, which reaches
  nobody (harness-facts.md:60): deletion candidates, the owner's call (Tooling F3).
- suggest-compact.sh: about 7 process spawns per tool call and a quadratic json_str (Efficiency
  F2).
- Commit gates cd to CLAUDE_PROJECT_DIR, so a worktree or `git -C` commit is judged against the
  main tree's index (Data flow).
- Stale text: suggest-compact.sh:107-108 points at harness-facts.md for a section in
  hook-authoring.md; validate-push.sh:16 "line 16"; "fall back to python3" comments in four hooks;
  _pybin.sh:141-145 cost note; tools/README.md:41 section list.
- UNVERIFIED: a git command sent through the PowerShell tool meets no Bash gate; whether a
  non-zero Bash exit fires PostToolUse or PostToolUseFailure.
- Trap to record: `$(</dev/stdin)` reads 0 bytes through a native Windows pipe while passing
  test_hooks.sh's Cygwin pipe; keep `INPUT=$(cat)` (Efficiency, considered and not raised).

VERDICT: READY FOR COMMIT (Step 4 complete except the 4.6 convergence pass owed by the
orchestrator; hook suite 341 passed, 0 failed; dotnet suite at the known baseline)
```

## Verification runs (this session, in the worktree)

| Run | Result | File under `E:\repos\taom-improve\scratch\013-review\` |
|---|---|---|
| Committed suite, before any change | 310 passed, 0 failed | `baseline.txt` |
| Committed suite, two hook mutations planted | 310 passed, 0 failed (RED: neither caught) | `red-old-test-mutated.txt` |
| New suite, same mutations, fake interpreter sleeping 1 s | 338 passed, 3 failed: the exit 3 and the two suggest-compact rows only | `green-new-test-mutated-slowfake.txt` |
| New suite, clean, during `dotnet test` | 341 passed, 0 failed | `green-under-load.txt` |
| Two new suites at once | 341 passed, 0 failed, each | `stress-a.txt`, `stress-b.txt` |
| `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | Failed 2, Passed 10235, Skipped 2 (the two known live-Armory tests) | `dotnet-test.txt` |

## CODEX REVIEW

Codex (gpt-6-astra, ultra) finished: `docs/reviews/raw/codex-adversarial-013-bash-hook-prefilter-2026-09-24.md`
ends with "END OF CODEX REVIEW". It read the branch through git refs only and ran nothing.
Quality: it cross-referenced every filter against its trigger line and every 4c identifier against
its source, and it disputed its own known suspects with line evidence. It could not see the
builder's logs, so it left the 4c flake UNVERIFIED.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | LOW | Yes | suggest-compact's `dotnet` and `build.ps1` arms had no row; the positive oracle proved only an interpreter start. Reproduced by mutation (F3). Its further ask, behaviour assertions for suggest-compact's boundary signals with controlled throttle state, is partly met by F2's section 4 payload and otherwise left as follow-up |
| 2 | P3 | LOW | Yes | "JSON never escapes an ASCII letter" is false of JSON; true of Claude Code's writer as far as #647 shows (F5). Its alternative, sending any `\u`-escaped payload through the parser, is a design choice for Mike |
| note | none | none | Yes | The plan's parity script compared stdout and exit code only. The Tooling lens's 300-case run closed that gap (stderr and marker compared, 0 differences) |
| KS4 | DISPUTED | MED | Partly | Codex disputed a narrowing defect correctly, but the STOP message did appear (`green.txt:193`); its cause is the oracle, not the prefilter (F1) |
| KS9 | CONFIRMED | LOW | Yes | Same as finding 1 |
| KS10 | DISPUTED | HIGH, pre-existing | Yes | The multi-line push blind spot is inherited, not introduced; follow-up with a Deferred trailer |

**Confirmed bugs:** Codex 1 (fixed, `tools/test_hooks.sh` 4c) and Codex 2 (fixed, 12 hook
comments, CHANGELOG, `hooks-catalog.md`). **False positives:** none. **Design questions:** whether
to route `\u`-escaped payloads through the parser. **Things Codex missed:** the timing-dependent
4c oracle (F1, the one MED), section 4's contract no longer reaching the parse path (F2), 4c's
discovery gap (F4), the catalog back-reference (F6).

### Phase 3e root cause

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| C1 | suggest-compact arms without trigger rows; oracle weaker than its label | Other: filter arm without a test row | The plan prescribed one row per event, not per filter arm, and the parity script excluded suggest-compact | Lesson "Give every arm of a fast-path filter its own row" in `lessons/build-tooling-workflow.md` |
| C2 | Serializer premise written as a JSON guarantee | Assumed an API worked a certain way without verifying | The plan's own caveat (plans/013:189) did not reach the prescribed comment text (plans/013:406) | Lesson "State a payload premise as the producer's behaviour" |

### AGENTS.md lessons (pending)

Phase 3h is consolidated later for all branches. Lines I would add:

- **Bugs Codex typically misses:** a test oracle that the code under test can silently replace
  (a pinned interpreter that a timed probe drops); Codex reads source and cannot see run logs, so
  a flake recorded only in a scratch log stays UNVERIFIED.
- **Bugs Codex typically misses:** an early-exit filter that moves an existing contract test's
  payloads off the path the test was meant to cover.
- **What Codex does well:** a per-hook table pairing each new filter with its downstream trigger
  line, and a concrete counter-payload (`\u0067it reset --hard`) for a stated premise.

RCA: `docs/reviews/rca-bash-hook-prefilter-2026-09-24.md`.

## Convergence

One deep-reviewer pass (standards plus behaviour parity) over `90141357..787fd366`. It found no
behaviour defect: the 12 hook edits change comment lines only, the new 4c trace check fails safe,
discovery still finds 13 rows, and the `bash-trigger` payload passes every prefilter without
tripping any hook's own check. It found three LOW text defects, each verified against the file
before fixing, and one NIT.

| # | Sev | Finding | Verdict | Fix |
|---|---|---|---|---|
| V1 | LOW | `CHANGELOG.md:16` and `:30` read "JSON allows `g`": the escape had been decoded into the letter it spells | CONFIRMED | Both lines now name the escape for `g`. Cause: the Edit tool decodes a backslash-u escape in its replacement text, and did so again on the first attempt at this fix; the fix went in through a script that builds the backslash with `chr(92)` |
| V2 | LOW | `hooks-catalog.md:22-23` gave 256 to 451 ms as the cost of the two Python starts; it is the whole hook's time on an `ls` before the change, bare-bash floor included (`scratch/013/step0-timing.txt`) | CONFIRMED | Now says a hook took 256 to 451 ms before the prefilter and 60 to 150 ms after (`step7-timing.txt`) |
| V3 | LOW | `REVIEW-LOG.md:3790` said both MEDs were proven by planted mutations; the mutations were the `exit 3` (F2) and the suggest-compact arm removal (F3), and F1 was proven by the 1 s slow fake. The RCA claimed a failing proof first for all six findings; F4 to F6 had none | CONFIRMED | Each proof attributed to the finding it proves, in both files; the RCA says F4 to F6 are discovery and wording fixes without a failing proof |
| V4 | NIT | `lessons/build-tooling-workflow.md:2274` said "Eleven gates" skip parsing without `git`; only the ten git gates do (`suggest-compact.sh:79` also accepts `dotnet` and `build.ps1`) | CONFIRMED | "The ten git gates" |

False positives: none. Not acted on (the reviewer's UNVERIFIED items): the #647 citation as proof
of Claude Code's payload writer, and the `re.fullmatch` matcher assumption; both wait on the live
proof owed after merge (plan 013:695).

Verification after the fixes, in the worktree:

| Run | Result | File under `E:\repos\taom-improve\scratch\013-review\` |
|---|---|---|
| `bash tools/test_hooks.sh` | 341 passed, 0 failed | `convergence-hooks.txt` |
| `python tools/lint_docs.py --dash-base 787fd366` | 0 new dashes; 7 size warnings, all on rules this branch does not touch | none |
| `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | Failed 2, Passed 10235, Skipped 2: the two known live-Armory tests, the same pair as `dotnet-test.txt` | `convergence-dotnet-test.txt` |

## Maintainer decisions applied (2026-09-24)

Applied on `improve/013-bash-hook-prefilter` over base `5dcef67a`, in one commit:
`fix(hooks): v2.0.30 - apply maintainer decisions for plan 013`, the commit that adds this
section (its hash is the branch head that carries it). No GitHub issue is cited: plan 013's
issue is not filed yet (F7).

| Decision | Review item it answers | What changed | Commit |
|---|---|---|---|
| D39 | NOT APPLIED: Design P2 / Efficiency F1 and Design P3 | Each gate prefilters on the word it gates: the six commit gates on `*commit*`, `validate-push.sh` on `*push*`, `block-no-verify.sh` on `*no-verify*`. `block-dangerous-git.sh` and `block-broad-git-add.sh` keep `*git*`, `suggest-compact.sh` is untouched (D42) | `fix(hooks): v2.0.30 - apply maintainer decisions for plan 013` |
| D40 | Phase 3d row 2, Codex's alternative | A payload holding any `\u` escape takes the full parse: `\|\| "$INPUT" == *'\u'*` on the prefilter line of the twelve prefiltered hooks | same commit |
| D38 | FOLLOW-UP HIGH: `validate-push.sh` reads only the first line | Handled in plan 011, which executes on top of this branch; nothing here | none |
| D41 | FOLLOW-UP MED: `mark-verification-run.sh` never marks the canonical test command | Handled in plan 011; nothing here | none |
| D42 | FOLLOW-UP: delete `suggest-compact.sh` and `notify-test-results.sh` | Handled in plan 011; nothing here. `suggest-compact.sh` is left exactly as it was, and `notify-test-results.sh` takes D40 until then | none |

**D39, the word each gate needs, read from its body.** The six commit gates
(`check-changelog-changed.sh`, `check-claude-files-tracked.sh`, `check-commit-subject-version.sh`,
`check-moduledata-validation.sh`, `check-native-dll-crt.sh`, `check-doc-config-drift.sh`) act only
past `*"git commit"* | *"git -"*" commit"*`; both arms hold `commit`, and the subject gate's
`bash -c` unwrapping still needs the word `commit` in the text. `validate-push.sh` acts only at a
token equal to `push`; `block-no-verify.sh` only on `--no-verify`. None needs more than its one
word. `block-dangerous-git.sh` and `block-broad-git-add.sh` judge reset, clean, branch, checkout,
restore, stash, add and `commit -a`, so `git` stays the narrowest word that reaches every case;
the decision named neither, and they are unchanged by it. `.claude/rules/hook-authoring.md` names
no prefilter literal (line 153 says only "after any raw-payload prefilter"), so it is unchanged;
`docs/reference/hooks-catalog.md` carries the words. Behaviour change the maintainer accepted: with
no usable Python, a narrowed gate prints its degraded warning only on a call holding its word.

**D40, where the condition lives.** The hooks share no prefilter: each tests the raw payload on its
own line, so the condition went into each of the twelve lines rather than into a new shared helper
(`simplicity-criterion.md`). The rule costs a parse on any payload with a `\u` escape, including a
tool response carrying colour codes (ESC has no short JSON escape, so it is written `\u001b`); it
can never skip one.

**Tests, written first.** `tools/test_hooks.sh`: 4c gives each narrowed gate `git status --short`,
`git diff --stat` and `git log --oneline -5` rows that must not source `_pybin.sh`, gives
`validate-push.sh` and `block-no-verify.sh` their own trigger rows, and feeds every prefiltered
hook but `suggest-compact.sh` its word with one letter escaped. The new 4d feeds five blocking
gates their blocked command plain and escaped (`git \u0063ommit -m "no label here"`,
`git \u0070ush --force origin master`, `git commit --\u006eo-verify -m x`, `\u0067it reset --hard`,
`\u0067it add -A`) and requires the same verdict. Section 4's `bash-trigger` payload and section
5's starved payload gained the new words, so the contract and the degraded branch still reach
every gate's parse path; with the old section 5 payload, `check-changelog-changed.sh` and
`block-no-verify.sh` printed no degraded warning after D39.

| Run | Result | File under `E:\repos\taom-improve\scratch\013\apply\` |
|---|---|---|
| `bash tools/test_hooks.sh`, before any change | 341 passed, 0 failed | `baseline.txt` |
| D39 RED, new rows only | 341 passed, 24 failed: the 24 git-call rows of the eight narrowed gates | `red-d39.txt` |
| D39 GREEN | 365 passed, 0 failed | `green-d39.txt` |
| D40 RED, new rows only | 365 passed, 17 failed: 12 escaped-word rows in 4c, 5 in 4d (each plain form gave its expected verdict) | `red-d40.txt` |
| D40 GREEN | 382 passed, 0 failed | `green-d40.txt` |
| Old (`HEAD`) versus new hooks, stdout and exit code, 12 hooks by 20 commands | `parity: 240 cases, 0 differences` | `parity-out.txt` |
| `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | 0 errors | `dotnet-build.txt` |
| `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | Failed 2, Passed 10235, Skipped 2: the two known live-Armory tests | `dotnet-test.txt` |
| `python tools/lint_docs.py --dash-base 5dcef67a` | 0 new dashes; 7 size warnings, all on rules this branch does not touch | `lint.txt` |
| `python tools/audit_claude_config.py --no-repo-secrets --min HIGH` | no findings at or above HIGH | none |

Still owed: the live proof after merge (plan 013:695). Add one case to it: a plain `git status`
must complete with no hook message.

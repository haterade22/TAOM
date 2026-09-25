# Deep review: plan 013 maintainer decisions D39 and D40 (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 013 maintainer decisions (D39 narrowed prefilters, D40 escape rule),
         branch improve/013-bash-hook-prefilter, diff 5dcef67a..7aa658e3
Date: 2026-09-24

Scope:   scripts (12 Bash hooks, tools/test_hooks.sh), harness docs (CHANGELOG,
         hooks-catalog.md, the first review record). No C#, XML or C++.
Waves:   one wave: Standards, Efficiency, Completeness, Data flow, Design, Tooling;
         Codex gpt-6-astra (reasoning ultra) in parallel, complete ("END OF CODEX REVIEW")

STANDARDS:     PASS with fixes: 2 LOW, 4 NIT, all text or test rows, all fixed
COMPATIBILITY: NOT IN SCOPE (no engine code)
EFFICIENCY:    PASS: 3 items (E1 MEDIUM cost-only, needs Mike; E2 LOW wording, fixed;
               E3 MEDIUM follow-up, needs Mike)
COMPLETENESS:  INCOMPLETE before fixes (documentation only); COMPLETE after, except the
               #661 citation the orchestrator adds when batch 3 is done with wt-013
DATA FLOW:     PASS: 24 CONNECTED, 0 behaviour defects, 1 gap (G1, fixed), 9 inconsistencies
               (I1 to I4, I6 to I9 fixed; I5 cost-only, needs Mike)
DESIGN:        2 KEEP proposals (P2 applied; P1 not applied, needs Mike)
XML:           NOT IN SCOPE
TOOLING:       PASS with fixes: 2 LOW, 4 NIT, all fixed
```

Every lens and Codex agree D39 and D40 are implemented correctly and completely: each narrowed
word is required by its hook's own trigger, the `\u` arm is on all twelve prefilter lines, and the
only changed decisions (Tooling's 408-case old-versus-new run) are the eight intended D40 cases
where the base filter missed an escaped letter. No finding changes a gate decision. The confirmed
findings are a false before-case and an overstated parity claim in the CHANGELOG, two test rows
that could not catch the mutant they exist for, and wording that overstated coverage, savings or
safety reasoning.

## Verification of each finding

Each finding was re-checked against the worktree this session before it was acted on. Where
several lenses raised the same defect it is listed once, with every source.

| # | Sev | Finding (sources) | Verdict | Evidence this session |
|---|---|---|---|---|
| R1 | LOW | CHANGELOG before-case `git \u0063ommit -m "no label here"` did not pass the base gate (S1, I1, Tooling F1, C1, Codex 1, Design note 1) | CONFIRMED, fixed | The subject gate's only hunk is the prefilter (`git diff 5dcef67a..7aa658e3`); the base line was `[[ "$INPUT" == *git* ]]`, which admits that payload and skips `\u0067it commit ...` (bash probe). Head denies both (ran the head hook) |
| R2 | LOW | "240 payload cases found no changed decision" while the entry documents a changed decision; the parity script held no escaped payload (I2, Tooling F1) | CONFIRMED, fixed | `grep -c u00 scratch/013/apply/parity.sh` is 0 |
| R3 | LOW | Commit-gate comment said the gates need `git commit`; the `git -C <dir> commit` arm holds only `commit`, and 4c had no row for it (S2) | CONFIRMED, fixed | Mutant: `check-changelog-changed.sh` narrowed to `*"git commit"*`; the committed suite stayed green on it (379 passed, 3 failed, all three on the other mutant) |
| R4 | LOW | 4c's default escaped row `git \u0063ommit` holds a literal `git`, so a `git`-filtered hook without the escape arm passes it (Tooling F2, C8, G1) | CONFIRMED, fixed | Mutant: `check-claude-files-tracked.sh` back on `*git*` with no escape arm; its escaped-word row passed on the committed suite |
| R5 | LOW | "each blocking gate" / "every hook" for 4d; 4d covers five of ten (N3, I6, Tooling N4, C7, Codex 2) | CONFIRMED, fixed | `tools/test_hooks.sh` 4d table has five rows |
| R6 | LOW | Catalog says the raw test rests on Claude Code's writer; after D40 it rests on JSON grammar, and the premise decides cost only (N2, I4, C6) | CONFIRMED, fixed | RFC 8259 section 7 as Codex cites it (short escapes: quote, backslash, slash, b f n r t); the escape arm is on all twelve lines |
| R7 | LOW | "`git status`, `git diff`, `git log` start no Python" ignores the description field the raw test also reads (E2, Tooling N3, Design note 2) | CONFIRMED, fixed (wording) | The prefilter tests `$INPUT`, the whole payload |
| R8 | LOW | D39's text names `suggest-compact.sh`; the commit leaves it untouched without saying so (I3, C4) | CONFIRMED, record fixed; NEEDS MIKE to confirm | `DECISIONS.md:45` (main tree) reads "`commit` for the six commit gates and suggest-compact" |
| R9 | NIT | "Fail open on escapes" means the opposite of the house term (N1, I9, Tooling N1, Design P2) | CONFIRMED, fixed | `harness-facts.md` "TAOM hooks fail open"; the same files print "Gate failed OPEN" |
| R10 | NIT | First review record says two gates went silent on the old section 5 payload; seven did (N4, I8, Tooling N2, C5) | CONFIRMED, fixed | `git push --force origin master` holds neither `commit` nor `no-verify` nor a `\u`, so the six commit gates and `block-no-verify.sh` exit before `_pybin.sh` (prefilter lines read) |
| R11 | NIT | "Visible change" for a stderr warning from an exit-0 hook, which reaches only the debug log (I7) | CONFIRMED, fixed ("Behaviour change") | `harness-facts.md` Visibility row |
| R12 | LOW | #661 exists; the record says "not filed yet" and nothing cites it (C2) | CONFIRMED; record annotated, citation deferred | `gh issue view 661`: created 2026-09-24T22:30:48Z; `PROGRESS.md:68` defers the citation until batch 3 is done with `wt-013` |
| R13 | LOW | #661 body carries the R1 claim and `\^[` where `\u001b` was meant (C3) | CONFIRMED; NEEDS MIKE (public issue) | `gh issue view 661 --json body`: line 33 holds the literal characters, no ESC byte |
| R14 | MEDIUM (cost) | The `\u` arm also opens on literal `\u` text in commands and possibly on a `\\u` path segment (I5, E1) | NEEDS MIKE (amends D40's wording) | `*'\u'*` matches `...\\uncapturable_heroes` (bash probe); whether live payloads carry such a `cwd` is UNVERIFIED. The catalog now names literal `\u` text as the common cost |

**False positives:** none. Design's "1 MB re-timing not re-run" (UNVERIFIED) was answered by the
Tooling lens's measurement (+12 to 28 ms per hook, under the plan's 150 ms limit).

## Fixes, test first where testable

- **R3, R4 (tests):** `tools/test_hooks.sh` 4c adds `cd /x\ngit -C /y commit -m x` to the default
  trigger rows, and its default escaped row becomes `cd /x\n\u0067it \u0063ommit -m x` (the
  confirm gates' own row folded into it). RED: with both mutants applied, the committed suite
  gave 379 passed, 3 failed, none of them on the two gaps; the new suite gave 386 passed,
  5 failed, including "never reached _pybin.sh on a trigger payload [cd /x\ngit -C /y commit -m x]"
  and "never reached _pybin.sh when its word is escaped [cd /x\n\u0067it \u0063ommit -m x]".
  GREEN after restoring the hooks.
- **R9, R6, R3, R7 (hook comments, text only):** the twelve escape comments now read "Never skip
  on an escape" with the JSON-grammar reason; the six commit gates name `git -C <dir> commit` and
  say "every Bash call without the word"; `validate-push.sh` says the same. The only changed
  lines in `.claude/hooks/` are comments (checked with `git diff`). Edited by script with the
  backslash built from `chr(92)`; all hooks stay LF.
- **R1, R2, R5, R7, R11 (CHANGELOG):** corrected in place in the D39/D40 entry, plus a new entry
  for this commit. The before-case bytes were checked with `od -c`.
- **R5, R6, R7 (catalog):** the escape paragraph states the grammar argument, the cost sources
  and 4d's five gates; the commit-gate sentence gains the description caveat.
- **R8, R10, R12 (first review record):** D39 row notes the suggest-compact deviation; the
  silent-gate count is seven; the F7 line notes #661.

## Runs

| Run | Result | File under `E:\repos\taom-improve\scratch\013\review\` |
|---|---|---|
| `bash tools/test_hooks.sh`, before any edit | 382 passed, 0 failed | `hooks-before.txt` |
| Committed suite, two mutants applied | 379 passed, 3 failed (neither gap caught) | `mutants-oldtests.txt` |
| New suite, two mutants applied | 386 passed, 5 failed (both gaps caught) | `mutants-newtests.txt` |
| `bash tools/test_hooks.sh`, after all edits | 391 passed, 0 failed | `hooks-after.txt` |
| `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | Failed 2, Passed 10235, Skipped 2: `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`, the two known live-Armory tests; the branch predates `a39a9c86` | `dotnet-test.txt` |
| `python tools/lint_docs.py --dash-base 7aa658e3` | 0 new dashes; 7 size warnings, all on path-scoped rules this branch does not touch | none |
| `python tools/audit_claude_config.py --no-repo-secrets --min HIGH` | 122 files scanned; no findings at or above HIGH (one MED, `mcp-npx-unpinned`, unrelated to this diff) | none |

## ACTION ITEMS

1. Mike: confirm the D39 deviation (suggest-compact left on its old filter until D42 deletes it).
2. Mike: decide E1/I5 (narrow the escape arm to `\u00` followed by 2 to 7) and Design P1 (the
   commit gates' raw test becomes their own trigger pattern).
3. Mike: edit #661's body (R1 before-case, the `\^[` typo).
4. Orchestrator: cite #661 in the CHANGELOG when batch 3 is done with `wt-013`.

```
─────────────────────────
IMPROVEMENTS (Step 4)
─────────────────────────
APPLIED:
- .claude/hooks/*.sh (12 files), escape comment "Fail open on escapes" -> "Never skip on
  an escape" with the JSON reason (Design P2, Standards N1, Data flow I9, Tooling N1).
  Comment only; hook suite 382/0 before, 391/0 after.
- tools/test_hooks.sh 4c default escaped row holds neither word (Tooling F2, APPLY);
  proven by the check-claude-files-tracked mutant, RED on the new row, GREEN restored.
- CHANGELOG.md D39/D40 entry, before-case and parity sentence (Tooling F1, APPLY);
  od -c shows the backslash-u bytes.
- tools/test_hooks.sh:556 and hooks-catalog.md 4d wording (Tooling N4, APPLY).
- CHANGELOG.md, hooks-catalog.md, commit-gate and validate-push comments: the description
  caveat on the savings claim (Efficiency E2, APPLY).
- First review record: seven silent gates (Tooling N2, APPLY).

NOT APPLIED:
- Design P1 (six commit gates prefilter on their trigger pattern `git commit` /
  `git -` ... ` commit` instead of `commit`): behaviour-CHANGING (which calls start
  Python and when the degraded warning prints) and a refinement of D39's chosen word:
  needs Mike.
- Efficiency E1 / Data flow I5 (escape arm narrowed to `\u00[2-7]`): changes D40's
  stated rule ("any \u escape"): needs Mike. The catalog now names the cost instead.

FOLLOW-UP (pre-existing code, not changed here):
- block-no-verify.sh:62 misses `git commit -n` (git's short form) and the abbreviation
  `--no-verif`; rc 0 at base and head. A fix must also widen the D39 `*no-verify*`
  filter and add a 4c row. No issue filed: filing is the maintainer's call.
- Efficiency E3: the two confirm gates could filter on their subcommand words (37.8% of
  the remaining starts). Behaviour of the degraded warning changes; needs Mike.
- tools/README.md:41 describes test_hooks.sh without 4c or 4d.
- No test checks mark-verification-run's marker decision both ways; D41 in plan 011.
- Plan 011 (D42) must also remove 4c's suggest-compact and notify arms and the catalog's
  "two PostToolUse hooks" and suggest-compact sentences; plan 011's body predates D38,
  D41 and D42 (routing lives in DECISIONS.md and plans/README.md).
- plans/013-bash-hook-prefilter.md:665 Done list expects 11 hooks on `git`; stale by
  design after D39.
- The harness-facts.md premise row deferred by the first review is no longer needed for
  safety; only suggest-compact.sh still depends on the writer.

VERDICT: READY FOR COMMIT
```

The skill's Step 4 convergence pass (one `deep-reviewer` on the applied diff) is not run here:
this delegate cannot spawn agents. The applied diff changes no code line in any hook (comments
only) and adds two test rows, each proven by a mutant; the orchestrator may run the pass.

## CODEX REVIEW

**Codex review** (gpt-6-astra, reasoning ultra, 134,663 tokens), file
`docs/reviews/raw/codex-adversarial-013-bash-hook-prefilter-decisions-2026-09-24.md`, complete.
It read git refs only and ran nothing. It traced every predicate to its `_pybin.sh` source and
its downstream trigger, disputed or left UNVERIFIED all ten known suspects with file:line
evidence, and found no P1 or P2.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | LOW | Yes | CHANGELOG before-case is the D39-only intermediate, not the base (R1). Its replacement payload is the one applied |
| 2 | P3 | NIT | Yes | Catalog and 4d header say "each blocking gate"; 4d covers five (R5) |

- **Confirmed bugs:** 1 and 2, both fixed (R1, R5).
- **False positives:** none.
- **Design questions:** none raised by Codex.
- **Things Codex missed:** R2 (parity overclaim), R3 (`git -C` trigger row), R4 (default escaped
  row that cannot fail its mutant), R6 (stale premise text), R7 (description field), R8
  (suggest-compact deviation), R9 ("fail open" wording), R10 (silent-gate count), R14 (escape
  arm cost). Codex read the tests' oracles (it checked 4d's plain-verdict guard) but ran no
  mutant, so a row that passes through another arm looked sound.

### Phase 3e root cause, Codex-found bugs

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | CHANGELOG before-case describes the RED intermediate | Other: evidence from the wrong revision | The builder took the example from `red-d40.txt`, a D39-applied, D40-missing tree, not from the base commit | Lesson "Prove a before-case against the committed base" |
| 2 | 4d coverage stated as every blocking gate | Other: coverage claim not counted | The prose was written from the design intent, not from the five-row table | Same lesson (count the rows before the claim) |

### AGENTS.md lessons (pending; Phase 3h consolidated later)

- What Codex does well: a per-hook predicate table tying each raw filter to its `_pybin.sh`
  source line and downstream trigger line; replacement payloads it proposes are correct.
- Bugs Codex typically misses: a test row that passes on the mutant it exists for because the
  payload satisfies another arm of the filter (a literal word beside the escaped one). Suggest
  asking Codex, for each new coverage row, which single-arm deletion the row would fail on.
- Bugs Codex typically misses: a changelog or parity claim whose evidence ran on a different
  tree state than the one it describes.

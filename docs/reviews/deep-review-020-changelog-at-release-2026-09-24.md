# Deep review: plan 020, CHANGELOG generated at release (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 020, generate the CHANGELOG at release time from commit bodies
         (branch improve/020-changelog-at-release, bec0389d..ef5b7ff4)
Date: 2026-09-24 (review lead pass 2026-09-25)

Scope:   scripts (tools/changelog_from_commits.py and its tests), harness (four hooks,
         settings.json Step 0, test_hooks.sh, rules, skills), docs. No C#, XML or XSLT.
Waves:   Standards, Efficiency, Completeness, Data flow, Design, Tooling; Codex adversarial.
         Compatibility and XML were not launched (no engine or ModuleData surface).

STANDARDS:     FAIL: 6 findings (1 MEDIUM, 5 LOW); 5 confirmed, 1 false positive
COMPATIBILITY: NOT IN SCOPE
EFFICIENCY:    PASS: 1 LOW (release-time read cost), applied
COMPLETENESS:  INCOMPLETE: no GitHub issue; hand-edit check blind to committed edits;
               instruction sweep missed live lines; release-process.md half updated;
               refusal rule and test gaps
DATA FLOW:     FAIL: 3 gaps (2 MEDIUM, 1 LOW), 5 inconsistencies
DESIGN:        6 KEEP proposals (6 apply, 0 follow-up)
XML:           NOT IN SCOPE
TOOLING:       FAIL: 6 findings plus 1 nit (1 MEDIUM, 5 LOW)
CODEX:         complete, 0 P1 / 2 P2 / 1 P3, all confirmed
```

## Verification of every finding

Each lens finding and Codex finding was re-read against the worktree before any change. Many
lenses reported the same defect; the table merges them. "Fixed" means fixed in the review
commit, with the test named.

| # | Sev | Finding | Reported by | Verdict | Resolution |
|---|---|---|---|---|---|
| C1 | MED | `session-start.sh` degraded banner says "four" python-only gates; there are five, and the one left out (`check-commit-subject-version`) produces the labels the generator groups by | Std F1, DF I2, Tool F2, Design P1 | CONFIRMED: the grep over `.claude/hooks/*.sh` for `taom_pybin_degraded` with no `jq` lists five files; the diff rewrote "Five" to "Four" by subtracting the retired gate from a list that was already one short | Fixed. Banner and comment name all five and carry no count. New `tools/test_hooks.sh` check 5b2 fails when a python-only gate is missing from the banner; it failed on the old banner (RED in the full suite run) and passes now |
| C2 | MED | Lens 4 item 5 runs `git diff --name-only HEAD -- CHANGELOG.md`, which cannot see a committed hand edit; in this worktree it printed nothing although the range rewrote the file | Std F4, Compl F1, DF I3, Tool F1 (docs side), Design P4 | CONFIRMED: reran both commands in the worktree | Fixed. The lens diffs against the review base, with the `/release` and archive-roll exemptions. `/verify` Step 5 stays HEAD-based: it runs before a commit, where that is correct |
| C3 | MED | A commit body line that reads as a Markdown heading lands verbatim in the generated file. A body showing `## v2.0.32 (` makes the later v2.0.32 run refuse as a duplicate; any `##` or `###` line in a body also adds a heading to the file's structure | Codex P2-1 | CONFIRMED: the new test fails with the duplicate `ValueError` on the old code | Fixed. `_contain` escapes body lines CommonMark reads as ATX headings (up to three spaces, one to six `#`, then a space or the line end). `#622: ...` issue references at a line start stay untouched, so output for both real ranges is byte-identical. Tests `test_render_escapes_a_body_line_that_would_be_a_heading`, `test_render_leaves_an_issue_reference_at_a_line_start_alone`, `test_insert_after_a_release_whose_body_showed_the_next_heading` |
| C4 | MED | `/release` reads a moving `HEAD`: a commit landing between Phase 4 (generate) and Phase 7 (tag `HEAD`) ends up in the tag but in no generated section, this one or the next | DF G1 | CONFIRMED by reading `release/SKILL.md` Phases 4 to 7 and the generator's range code; that a concurrent commit lands mid-release is UNVERIFIED as a frequency, but `release-process.md` step 1 expects other sessions' edits | Fixed. The generator resolves `--until` to a SHA once, uses it for `describe` and `log`, and prints `ending at <sha>`. `/release` Phase 6 checks `HEAD` still equals it (else restore and regenerate); Phase 7 tags the release commit by SHA; `release-process.md` steps 7 and 8 match. Test `test_main_names_the_commit_the_range_ends_at` |
| C5 | MED | Live instructions still direct a hand edit of `CHANGELOG.md`: `troop-progression.md:84`, `gondor-ithilien-ranger.md:153`, `multi-culture-armor-revamp.md:94`, `gui-sprite-system.md:162`, `TEMPLATE.md:91`, `commit-split` row `:45`, `new-creature-mount:92`, lens 6 `:29`, lens 7 `:44`, `git-and-commits.md:67`, `v1.5.2-impact.md:199`; `external-skill-ports.md:132` claims a now-deleted hook catches it | Codex P2-2, DF G3, Compl F2 | CONFIRMED: each line read. The plan's sweep searched a directory list for fixed phrases and skipped `docs/features/` | Fixed, one line each (CRLF kept). `new-creature-mount`, which the plan deferred, is clean in the main tree now. `elephant.md:687` is NOT edited: the main tree holds another session's uncommitted edits to that file |
| C6 | LOW | `release-process.md`: writes the note (step 4) before generating its source (step 5); duplicate "4."; "every commit" where the tool reads non-merge commits; the `v2.0.12` crash-report link points at a file that no longer holds them | Codex P3, Std F2, DF I1 and I4, Tool F6, Compl F3, Design P5 and P6 | CONFIRMED | Fixed. Steps renumbered 1 to 8 in the skill's order, "non-merge", the link repointed to the H2 archive, and "Step 7 is the one that gets skipped" became Step 8. No other file cites these step numbers |
| C7 | LOW | `insert_section` refuses only a hand-written `## ` above the releases; a `###` or `####` entry, the level the old entries used, is accepted and kept above the new section | Compl F4, Tool F1 (part), Design P2 | CONFIRMED in memory against the committed header | Fixed. The first heading of level 2 or deeper must be a release heading. The refusal message now says to fold entries into the release note (committed bodies cannot change). Test `test_insert_refuses_a_hand_written_subheading_above_the_releases` |
| C8 | LOW | `VERSION_ARG_RE.match` accepts `v2.0.31\n`, which would write a broken heading | Tool nit | CONFIRMED | Fixed with `fullmatch`. Test `test_main_rejects_a_version_with_a_trailing_newline` |
| C9 | LOW | Five em dashes on lines this change touched (`finish-branch:3`, `:51`, `gui-ui.md:40`, `external-repo-adoption.md:34`, `hooks-catalog.md:52`) | Std F5 | CONFIRMED by `lint_docs.py --dash-base bec0389d` | Fixed (`gui-ui.md:40` held three). The same check now reports none outside the archive |
| C10 | LOW | `LABEL_RE` copies the gate's pattern with only a comment linking them | Std F6 | CONFIRMED (no test) | Fixed with Design P3: `LABEL_RE` is now the gate's pattern plus one named group, and `LabelParityTests` reads the hook's `pat` and checks both accept and reject the same ten subjects |
| C11 | LOW | Documented behaviours no test pins: the BOM-tolerant read and BOM-free write, UTF-8 stdout, the no-tag exit 2, alphabetical order of other types | Tool F4 and F5, Compl F4 | CONFIRMED (the tooling lens's mutants survived) | Characterisation tests added; all pass on the old and new code: `test_main_reads_a_bom_and_writes_without_one_keeping_crlf`, `test_main_prints_non_ascii_bodies_as_utf8`, `test_main_refuses_a_repo_with_no_release_tag`, `test_render_orders_other_types_alphabetically` |
| C12 | LOW | The `/release` description omits the CHANGELOG step, although `/release` is now the file's only writer | Compl F3 nit, Std follow-up | CONFIRMED | Fixed (26 words, still quoted YAML) |
| C13 | MED | The executor wrappers `plans/_audit/2026-09-23-opus/machinery/x-exec.js:41` and `x-followup.js:30` still order hand-written CHANGELOG entries; after the merge every dispatch writes the section the generator refuses | DF G2, Tool follow-up 1, Compl follow-up 3 | CONFIRMED by reading both lines | NOT fixed here: orchestrator machinery, outside the plan's scope and in live use. Listed under "For the orchestrator" |
| C14 | LOW | No GitHub issue for plan 020 | Compl | CONFIRMED (`gh issue list` in the lens) | NOT fixed here: the plan assigns it to the orchestrator, and `/issue` is never auto-invoked |
| F1 | LOW | Rules and skills copy "the commit body is the changelog entry" instead of linking | Std F3 | FALSE POSITIVE | Each hit replaces an existing step in place with the step's content ("describe it in the commit body") plus a clause of why. No AGENTS.md rationale is copied, and a step list needs the row. The plan prescribed the wording |
| F2 | LOW | Exit 1 with a traceback on a missing `CHANGELOG.md` or missing `git`, not the documented 2 | Tool F3, DF I5 | FALSE POSITIVE as a defect | The docstring's exit codes list refusals; a crash on a missing file is loud and names the file. Codex recorded it as outside the handlers, not a finding. Catching it adds code for no reader (simplicity criterion) |
| F3 | LOW | `VERSION_ARG_RE` and `RELEASE_HEADING_RE` accept only three-part versions while the gate allows four | DF I5 | FALSE POSITIVE | Release tags are three-part by contract (`release/SKILL.md` Gotchas); Codex agreed |

## Details by lens

**Standards (Agent 1):** C1, C9, C10, C6 and C2 confirmed and fixed; F1 false positive. H1 to
H6 passed in the lens (descriptions, harness facts, hook counts, budgets, tracked files).

**Efficiency (Agent 3):** one LOW, the release-time read of a 150 KB section; applied (below).
Hook retirement is a net saving (28 to 26 registrations).

**Completeness (Agent 4):** C14 (issue), C2, C5, C6, C7, C11 and C12 confirmed. The CHANGELOG
switch-over itself, the archive blob (`f512a0c9` equals `bec0389d:CHANGELOG.md`) and the IoC
N/A were verified.

**Data flow (Agent 5):** G1 is C4, G2 is C13, G3 is C5; I1 and I4 are C6, I2 is C1, I3 is C2,
I5 is C8, F2 and F3. 19 flows traced, including the label regex and the retired hooks.

**Design (Agent 6):** P1 = C1, P2 = C7, P4 = C2, P5 and P6 = C6, all applied as defect fixes. P3
applied as a behaviour-preserving deletion (below).

**Tooling:** F1 split: docs side is C2, the `###` case is C7, the generator-side
"edited since the last tag" guard is NEEDS MIKE (below). F2 = C1, F3 = F2, F4 and F5 = C11,
F6 = C6, nit = C8.

## Action items

1. (Done) C1 to C12 fixed in the review commit.
2. Orchestrator: update `x-exec.js:41` and `x-followup.js:30` so executors write the commit body,
   not a CHANGELOG entry, before the next dispatch after this merge (C13).
3. Orchestrator: file the plan 020 GitHub issue (C14).
4. Orchestrator: plan 011's branch turns `check-changelog-updated.sh` into a blocking Stop hook
   and registers, catalogs and tests it. Whichever order they merge, all four pieces must go.
5. Orchestrator: `elephant.md:687` still says "CHANGELOG before commit"; edit it once the other
   session's uncommitted edits to that file land.
6. Mike: the four NEEDS MIKE items below.
7. Convergence pass owed: Step 4.6's single `deep-reviewer` over the fix diff could not run here
   (a review lead cannot spawn agents).

## Improvements (Step 4)

APPLIED:
- `tools/changelog_from_commits.py:28-30,50,63` (Design P3, PRESERVING): `Commit` keeps only
  `sha subject body type`; `LABEL_RE` is the gate's pattern plus a `type` group; the if/else is one
  line. Proof: the 22 original tests (two parse tests narrowed to the fields that remain) and the
  real-history smoke. `v2.0.29..v2.0.30` still hashes to
  `ad0033656abef0cd579e59387534419feee4fcf6cfc5fd0a6a828b3beeeefb3f`, and the pending
  `v2.0.30..ef5b7ff4` section is byte-identical between the old and new generator
  (`82c4bb51...`).
- `.claude/skills/release/SKILL.md` Phase 5 (Efficiency issue 1, PRESERVING): list the section's
  group and subject lines with `awk` before opening bodies. Doc only; the lens ran the command on a
  simulated two-release file.

NOT APPLIED:
- Generator-side refusal when `CHANGELOG.md` changed since the previous tag (Tooling F1 tool side):
  behaviour-changing, and an archive roll edits `CHANGELOG.md` between tags too, so the guard as
  prototyped would refuse the release after every roll. Needs Mike.
- Default `--version` from `SubModule.xml`, or refuse a version that is already a tag (DF I5 and
  F5, Tooling follow-up 7): behaviour-changing; Design rejected it under the simplicity criterion.
  Needs Mike.
- Tooling F3 exit codes: see F2 above (simplicity criterion).
- Std F3 pointers instead of the step clause: false positive (F1).

FOLLOW-UP (pre-existing, outside the changed lines; no issue filed, because `/issue` is never
auto-invoked and the orchestrator batches issues):
- `tools/lint_docs.py` dash check (`_changed_markdown_lines`) does not skip
  `CHANGELOG_ARCHIVE_DIR`, so a branch-wide `--dash-base` buries real hits under 1,544 archive
  lines; the `ef5b7ff4` body's "the move adds no lint findings" holds only for the default base.
- `check_doc_graph_ratchet.py` fails identically at `bec0389d` (121 orphans against 26), and CI
  `build.yml:108` runs it.
- Pointers to the root CHANGELOG for content that moved to the archive: `career-system.md:569`,
  `localization-override.md:125`, `offspring-race-inheritance.md:58`, `weather-bounds-guard.md:126`,
  `advanced-start-options.md:11,114`, `map-load-diagnostics.md:11`, `kitbash/lond-cirion-walls.md:77`,
  `editing-safely.md:70` (already wrong at base), `external-repo-adoption.md:46-47`,
  `Main/Features/NavalTravel/CHANGELOG.md:3-4`.
- The fixture-repo tests (this file and `test_reviewctl.py`) do not clear `GIT_DIR`,
  `GIT_WORK_TREE` or `GIT_INDEX_FILE`; whether a git hook would leak them in is UNVERIFIED.
- `/release` phase headings mix two styles (a dash in Phases 1 to 3, 6 and 7; a colon in 4 and 5); the `ship`, `finish-branch` and
  `new-adr` descriptions are summaries, not "Use when".
- Unlabelled commits (9 of 157 since v2.0.30): the CI or git-hook subject check the plan deferred.
- `audit_claude_config.py` HIGH at `tools/tests/test_audit_repo_secrets.py:178`; lint's missing
  feature doc for `TrollBruteForce`. Neither is from this change.

## NEEDS MIKE

1. Should the generator refuse when `CHANGELOG.md` changed since the previous tag (which catches a
   hand edit inside an older section), and if so how does an archive roll get through it?
2. Should `--version` default to `SubModule.xml`'s version, or refuse a version already tagged?
3. `bannerlord-1.4.5` still registers both retired hooks, keeps "CHANGELOG.md every session" and
   has no generator, while `/release` Phase 1 names it a release branch. Port plan 020 there, or
   record that the 1.4.5 line keeps the hand-written file?
4. Plan 011 makes `check-changelog-updated.sh` a blocking Stop hook; plan 020 deletes it. Confirm
   020's retirement wins at merge (the plan says so; this is the product call behind item 4 above).

## Settings changes for the orchestrator

None. The `.claude/settings.json` diff is the approved Step 0 (two registrations removed); the
lenses and Codex counted 26 remaining registrations across 9 events, each with a script on disk
and a timeout.

## AGENTS.md lessons (pending, for the consolidated Phase 3h)

- What Codex does well: it found the two-release interaction (a body example colliding with the
  next run's duplicate check) that six lenses missed, by walking a scenario across two
  invocations instead of one.
- What Codex does well: it read the plan's own sweep scope and showed the gap in it
  (`docs/features/` omitted), not just a stray line.
- Bugs Codex typically misses: a hand-kept list in a hook message that no longer matches the
  hooks (the banner); a `git diff HEAD` check that cannot see committed edits; a range read from a
  moving `HEAD` across a multi-step release.
- False positives: none this review.

## Verification run by the lead

- `python tools/tests/test_changelog_from_commits.py`: Ran 33 tests, OK (22 before; 11 new. Five
  failed first on the old code, six failure lines counting subtests; the other six, the label
  parity, BOM, UTF-8, no-tag, alphabetical-order and issue-reference cases, pass on both).
- `bash tools/test_hooks.sh`: 373 passed, 0 failed (368 at `ef5b7ff4` plus the five 5b2 checks;
  5b2 failed on the old banner in an earlier full run).
- `python -m unittest discover -s tools/tests -t .`: Ran 1952 tests, 3 failures, the same three
  the Tooling lens saw at `ef5b7ff4` before any fix: `test_check_generator_item_refs` twice (live
  Armory data) and `test_clan_heraldry_specs ... test_applying_every_spec_is_a_no_op` (the known
  CRLF worktree failure). None reads a file this review changed.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (branch contains `a39a9c86`):
  Failed 0, Passed 10629, Skipped 2, Total 10631.
- `python tools/lint_docs.py --fail-on-drift`: exit 0. `--dash-base bec0389d`: no dash outside
  the archive. `python tools/reviewctl.py lint`: "Shared contract OK: 10 lanes".

VERDICT: READY FOR COMMIT (Step 4 complete except the convergence pass, which a review lead
cannot spawn; the NEEDS MIKE items and the orchestrator actions do not block this commit).

## CODEX REVIEW

Source: `docs/reviews/raw/codex-adversarial-020-changelog-at-release-2026-09-24.md` (complete,
"END OF CODEX REVIEW" present; 95,017 tokens). Codex reviewed the git objects
`bec0389d..ef5b7ff4` read-only, executed nothing, answered all ten Known Suspects and
cross-referenced the settings registrations, the label pattern, the archive blob and the module
versions.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | MED | Yes | A body line `## v2.0.32 (` is matched by the duplicate scan; the RED test reproduced the refusal. Wider than stated: any heading-shaped body line also adds structure to the file. Fixed by escaping (C3) |
| 2 | P2 | MED | Yes | The three feature recipes it names still say to update CHANGELOG, and the plan's scan omitted `docs/features/`. The lenses found eight more such lines; all fixed but `elephant.md` (C5) |
| 3 | P3 | LOW | Yes | `release-process.md` order contradicts the skill (C6) |

Known Suspects: all ten dispositions check out against the worktree. On suspect 6 Codex was
right to mark the gates UNVERIFIED; the lead ran them (above).

**Confirmed bugs:** 1, 2 and 3, fixed as C3, C5 and C6.
**False positives:** none.
**Design questions:** none raised by Codex; the lenses' four are under NEEDS MIKE.
**Things Codex missed:** C1 (banner count), C2 (lens blind to committed edits), C4 (moving `HEAD`
across the release), C7 (`###` hand entries), C8, C13 (executor wrappers) and the unpinned
design points (C11).

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Body heading breaks a later release | Other: unescaped free text in a parsed file | The plan prescribed verbatim bodies and a whole-file regex; every fixture body was plain prose and every test ran one release | `_contain` plus a two-release test; lesson in `lessons/build-tooling-workflow.md` |
| 2 | Live recipes still order CHANGELOG edits | Convention inconsistency | The retirement sweep was a directory list and fixed phrases (repeat of the 2026-07-01 living-docs lesson) | Lines fixed; lesson in `lessons/build-tooling-workflow.md` |
| 3 | Contract doc order contradicts the skill | Convention inconsistency | The step text was replaced in place without re-reading the step order | Fixed; covered by lesson 2 (a skill and its "Full contract" doc move together) |

RCA: `docs/reviews/rca-changelog-at-release-2026-09-24.md`.

# Input pack: WP 102 changelog-at-release

Title (draft): Adopt plan 020 on 1.4.x: the commit body is the changelog entry and /release generates CHANGELOG.md
Wave: 0   Lane: harness   Draft priority: P2   Groups: 1   Port commits: 7
Depends on (WP ids): 101

## Orchestrator notes for this WP (binding)
- Decision D1: ADOPT. Land before WP103 so the CHANGELOG gates are deleted rather than switched on by the output-contract fix.
- Also take 9bfd0f411 (K2: tools/changelog_from_commits.py fix #1009) after ae85381dc, 6e4bfedb7, 971d8e975, 5b242cee6.
- Version decision D8: the next 1.4.x release is v2.4.0 (3-part, accepted by the generator regexes unchanged); the tool must report the commits since v2.0.28.
- settings.json edits (deleting the two CHANGELOG hook registrations) are the maintainer's: give the exact JSON diff.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D2-harness-and-changelog.md
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md

## Deep-dive findings and non-port items this WP owns
- RELEASE-INTEGRITY-09 [P2/high] Port the release process before the port wave: plan 020 removes the CHANGELOG conflict class and the 1.4.x hooks that would force a hand edit per cherry-pick | port: c91bb95f8,30b54df75,ef5b7ff4e,6e4bfedb7,971d8e975,5b242cee6,55a729152  (report: plans/_audit/2026-11-04-port148/deepdive/release-integrity.md)

## Port groups owned by this WP (from the commit classification)

### group `plan-020-changelog-at-release`  P2  track=dev-tools  commits=7  sim: clean=0 conflict=7
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/changelog_from_commits.py, tools/test_hooks.sh, tools/tests/test_changelog_from_commits.py
- `ae85381dc` 2026-09-25 UNSURE P2 pick=conflict :: feat(tools): v2.0.30 - CHANGELOG release section from the commit log
  - why: Adds tools/changelog_from_commits.py, which builds a CHANGELOG release section from version-labelled commit subjects and bodies; the first step of retiring the hand-edited CHANGELOG.
  - port notes: Decided by the maintainer for the whole plan 020 series (ae85381dc to 5b242cee6). (a) Does the 1.4.x line adopt 'the commit body is the entry'? Its practice already lapsed: none of the five 1.4.x commits after 2026-09-15 (f8c9c8ecb, 55d779012, d906fc921, ccef172ad, 1b00d1acd) touched CHANGELOG.md, whose newest section is 2026-09-15, while its hooks and docs still demand hand edits. (b) The 1.4.x release version scheme: v2.0.29 to v2.0.36 are 1.5.x tags, the newest tag reachable on 1.4.x is v2.0.28, and the generator's VERSION_ARG_RE (and RELEASE_HEADING_RE) accept only vX.Y.Z. If adopted, port the series as one unit and early, because it decides whether every later port carries a CHANGELOG hunk. The tool and its tests are new files; only the tools/README row conflicts.
- `c91bb95f8` 2026-09-25 UNSURE P2 pick=conflict :: chore(hooks): v2.0.30 - retire the two CHANGELOG hooks and their rules
  - why: Deletes check-changelog-changed.sh and check-changelog-updated.sh and the rule text that cited them, since /release generates CHANGELOG.md.
  - port notes: Only if plan 020 is adopted (see ae85381dc). Both hooks are registered on the 1.4.x tip (PreToolUse Bash and Stop); the maintainer removes the settings.json registrations by hand because config-protection keeps agents out. session-start.sh and tools/test_hooks.sh need adapting: the PF_NARROWED_LIST edit assumes plan 013's prefilter (90141357d).
  - prereqs: ae85381dc
  - conflicts: tools/test_hooks.sh
- `30b54df75` 2026-09-25 UNSURE P3 pick=conflict :: docs(workflow): v2.0.30 - the commit body is the changelog entry
  - why: Rewrites every instruction that said to update CHANGELOG.md (AGENTS.md, CLAUDE.md, rules, skills, guides, the Serena checklist) to say the commit body is the entry and /release generates the file.
  - port notes: Only if plan 020 is adopted. Re-apply to the 1.4.x copies; drop hunks for files absent there (.claude/skills/armory-audit/SKILL.md, .claude/skills/deep-review/lenses/4-completeness.md) unless their creating commits are ported first.
  - prereqs: ae85381dc, c91bb95f8
- `ef5b7ff4e` 2026-09-25 UNSURE P3 pick=conflict :: chore(changelog): v2.0.30 - archive the hand-written CHANGELOG
  - why: Moves the hand-written CHANGELOG.md into docs/changelog-archive/ and leaves a header-only CHANGELOG.md saying the file is generated at release.
  - port notes: Only if plan 020 is adopted, and never as a cherry-pick: it carries the 1.5.x CHANGELOG blob. Re-cut from origin/bannerlord-1.4.5's CHANGELOG.md (19,381 lines, newest section 2026-09-15) into a new archive beside docs/changelog-archive/CHANGELOG-2026-H1.md, then write the header.
  - prereqs: ae85381dc
- `6e4bfedb7` 2026-09-25 UNSURE P2 pick=conflict :: fix(changelog): v2.0.30 - review follow-ups for plan 020
  - why: Review fixes for plan 020: commit bodies can no longer inject CHANGELOG headings, /release re-checks HEAD and tags by SHA, the refusal catches hand-written ### entries, and leftover instructions point at the commit body.
  - port notes: Only if plan 020 is adopted. Take the generator fixes and 11 tests, the /release flow checks and the session-start banner with test_hooks.sh 5b2. Drop hunks for 1.5.x-only files (docs/migration/v1.5.2-impact.md, deep-review lenses 6-design.md and 7-xml.md, docs/ai-includes/git-and-commits.md). The label-versus-subject-gate parity test reads check-commit-subject-version.sh, which differs between the lines; adapt it or port the 1.5.x gate first.
  - prereqs: ae85381dc, c91bb95f8, 30b54df75
  - conflicts: tools/changelog_from_commits.py, tools/test_hooks.sh, tools/tests/test_changelog_from_commits.py
  - issues: 622
- `971d8e975` 2026-09-25 UNSURE P2 pick=conflict :: fix(changelog): v2.0.30 - convergence fixes for plan 020
  - why: Convergence fixes: /release checks that the release commit's parent is the generator's range end before tagging, and test_hooks.sh 5b2 reads gate names from code, not comments.
  - port notes: Only if plan 020 is adopted; follows 6e4bfedb7.
  - prereqs: 6e4bfedb7
  - conflicts: tools/changelog_from_commits.py, tools/test_hooks.sh
- `5b242cee6` 2026-09-25 UNSURE P3 pick=conflict :: fix(changelog): v2.0.30 - final convergence fixes for plan 020
  - why: Final LOW convergence fixes for plan 020: generator docstring, RCA rows, test_hooks.sh 5b2 robustness and the module-taom.md release step.
  - port notes: Only if plan 020 is adopted; follows 971d8e975.
  - prereqs: 971d8e975
  - conflicts: tools/changelog_from_commits.py, tools/test_hooks.sh
skipped in this group: `96afb6fb4` SKIP-BOOKKEEPING docs(plans): v2.0.30 - plan 020 merged, the sprint's last branch

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 9

---
paths:
  - "TAOM.Tests/**"
  - "**/*Tests.cs"
  - "**/*Test.cs"
  - "tools/tests/**"
---

# Testing Rules (TDD Mandatory)

## Workflow: RED -> GREEN -> REFACTOR
1. Write a failing test FIRST (verify RED state)
2. Write minimum production code to pass (GREEN)
3. Refactor while keeping tests green

Judge every test by the two-edit test: break the behaviour (it must fail), change only the internals (it must pass); see `4-completeness.md` item 9 in `.claude/skills/deep-review/lenses/`.

For `tools/tests/**` (Python `unittest`), this file applies and `tests.md` does not: a test
method must be named `test_*` or it never runs.

A gate (a test that lints or audits the repo) is proven on the states it will meet, not the one it
was written in: a fresh checkout (`git check-attr text eol` on every file its tool reads), an
untracked new file, a deleted one, a non-ASCII path, a folder whose case on disk differs from git's
(so a path key compares with `OrdinalIgnoreCase`), a run from a subfolder, a missing tool, and an
empty input that must not read as a pass (lesson in `docs/reviews/lessons/build-tooling-workflow.md`).

## Coverage Requirements

Every decision path in a service or engine has a test, judged by the two-edit test. No coverage percentage is measured or claimed.

A test comment or failure message that states an effect (what breaks, what imports, what the engine then does) rests on the line that produces the effect, read at both ends of the data flow. A production comment, a plan or a report is a claim, not that line (`docs/reviews/rca-external-mod-contract-tests-2026-11-13.md`).

A source-text test pins wiring only: this entry point calls that function, with these inputs, in this order. The decision itself goes in a pure type with behaviour tests. Check each pin against the cheapest edit that brings the defect back and keeps the pinned text (`docs/reviews/rca-muster-captain-2026-11-13.md`).

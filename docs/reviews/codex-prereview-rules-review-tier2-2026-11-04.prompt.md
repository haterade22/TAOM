# Codex review: the rules review's gates and tools, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls, and run no game.
Read `AGENTS.md` first. Do not write to the game install, and do not read or write `E:\LOTRAOM_Releases`. Mark
anything you cannot prove from code as UNVERIFIED.

The known red test `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is not a finding.

**Scope:** `git diff f3653f9bb..49bf62fea` of this repository, limited to `.githooks/`, `.claude/hooks/`,
`tools/**/*.py`, `tools/**/*.sh`, `tools/**/*.ps1`, `TAOM.Tests/` and `.claude/settings.json`. Both prior reviews of this
range ran without Codex. Later commits may already have changed a file: judge the current tree, and do not report what
a later commit fixed. Docs in the range are out of scope.

## Questions to answer with evidence

1. **Commit gates (`.githooks/pre-commit`, `pre-merge-commit`, `commit-msg`).** Can a bad commit pass? Check a path
   commit, a merge, an amend, a commit from a sibling worktree, a commit from a GUI, CRLF files and paths with
   spaces. Can a good commit be refused, or a hook fail open when it should refuse, or the reverse?
2. **`tools/data_gates.py` and the new validator codes.** Does each gate fail on the fault it names? Is there a
   neighbouring fault it passes with? Does any code read stale or partial data?
3. **Ratchets (`EntryPointLineCountGateTests`, `ServiceSignatureGateTests`, the doc and rule ratchets).** Can a
   violation pass because of the baseline, or how files are found?
4. **`block-no-verify.sh`, `_shellwords.py`, `config-protection`, `session-start.sh`.** Find a spelling that passes and
   should not, and one refused that should pass. Deliberate bypass by a person is out of scope; an accidental one is
   in scope.
5. **Retro and lint tools (`harness_retro.py`, `lint_docs.py`, `check_provenance.py`).** Does any number they print
   mislead, and can any exit 0 when it failed to read its input?
6. **Tests.** Does a new test pass with the fault it names? Name the deletion that would leave it green.

## Report

List findings by severity. For each: file and line, the concrete failing input, the evidence, and CONFIRMED or
UNVERIFIED. Say what you read and what you did not. If you find nothing in an area, say so.

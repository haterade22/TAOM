# Agent 4 lens: Completeness Check

Review the current work session for completeness. Check ALL of these:

FILES: the list in your spawn prompt.

1. **Tests Exist:** For every new service/behavior/model class in Main/Features/, verify a corresponding test file exists in TAOM.Tests/Features/. Flag any untested classes.
2. **Test Coverage:** Read each test file. Are edge cases covered? Are there tests for error/null/empty cases? Is the AAA pattern used (Arrange/Act/Assert)?
3. **Feature Doc:** If this is a new feature, check that docs/features/<name>.md exists. If not, flag it as MISSING.
4. **GitHub Issue:** Run `gh issue list --state all --limit 20` and check if there's an issue for this work. If not, flag as MISSING.
5. **CHANGELOG.md untouched:** only `/release` writes it, from commit bodies. Diff against the review's base, not `HEAD`, so a committed edit shows too: `git diff --name-only <base> -- CHANGELOG.md` (the base is `HEAD` for uncommitted work, else the start of the range) must print nothing. A hand edit is a defect; a `/release` run or an archive roll into `docs/changelog-archive/` is not.
6. **IoC Registered:** Check that new services/adapters are registered in DryIoc. Read the relevant IoC.cs file.
7. **SubModule.xml:** If new behaviors or models were added, verify they don't need SubModule.xml registration (most don't, but check).
8. **No gate turned green by editing the gate:** a removed `[TestMethod]`, an added `[Ignore]`, a weakened `Assert`, or a new validator allowlist entry (`_BY_DESIGN`, `_EXEMPT`, `exempt_troops` and the like) must say why in the entry or the commit body. Without a reason it is a finding: the check was silenced, not passed. The mechanical part: `git diff <base> -U0 | grep -nE '^-\s*\[TestMethod|^\+\s*\[Ignore|^\+.*(_BY_DESIGN|_EXEMPT|exempt_troops|KNOWN_FAILURES|shellcheck disable=|noqa|NuGetAuditSuppress|ruff: noqa)'`, plus, over every untracked file (`git ls-files --others --exclude-standard`, which `git diff` never shows), the same pattern without the `^[-+]` anchors: `grep -nE '\[Ignore|_BY_DESIGN|_EXEMPT|exempt_troops|KNOWN_FAILURES|shellcheck disable=|noqa|NuGetAuditSuppress'`; and any new line in `.gitleaksignore` (#1020); a weakened `Assert` still has to be read.

9. **Two-edit test check:** judge each new or changed test by two edits. Break the behaviour: the test must fail, and one that still passes proves nothing (causes: the expected value is computed by the code under test, the assertion checks data the test itself built, a restated constant). Change only the internals: the test must still pass, and one that fails is pinned to the implementation. A `DidNotReceive()` assertion needs a paired positive case that shows the path can call it. A self-check or verdict rule needs one test on evidence its real producers made, in the engine's call order, that PASSes it, and one injected fault that FAILs it; a hand-built evidence record proves only the rule's arithmetic (`rca-fords-of-war-2026-10-31.md` addendum).

OUTPUT FORMAT:
- ✅ Tests: [X test files, Y test methods]
- ✅/❌ Feature Doc: [exists at path / MISSING]
- ✅/❌ GitHub Issue: [#N title / MISSING]
- ✅/❌ CHANGELOG.md: [untouched / HAND-EDITED]
- ✅/❌ IoC: [registered / MISSING registrations]
- ✅/❌ Gates: [no silenced test or allowlist entry / list each one with no stated reason]

Overall: COMPLETE / INCOMPLETE — [list what's missing]

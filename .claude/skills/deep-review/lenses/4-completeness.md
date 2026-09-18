# Agent 4 lens: Completeness Check

Review the current work session for completeness. Check ALL of these:

FILES: the list in your spawn prompt.

1. **Tests Exist:** For every new service/behavior/model class in Main/Features/, verify a corresponding test file exists in TAOM.Tests/Features/. Flag any untested classes.
2. **Test Coverage:** Read each test file. Are edge cases covered? Are there tests for error/null/empty cases? Is the AAA pattern used (Arrange/Act/Assert)?
3. **Feature Doc:** If this is a new feature, check that docs/features/<name>.md exists. If not, flag it as MISSING.
4. **GitHub Issue:** Run `gh issue list --state all --limit 20` and check if there's an issue for this work. If not, flag as MISSING.
5. **CHANGELOG Updated:** Check if CHANGELOG.md has been modified with an entry for this work. Use `git diff --name-only -- CHANGELOG.md`.
6. **IoC Registered:** Check that new services/adapters are registered in DryIoc. Read the relevant IoC.cs file.
7. **SubModule.xml:** If new behaviors or models were added, verify they don't need SubModule.xml registration (most don't, but check).

OUTPUT FORMAT:
- ✅ Tests: [X test files, Y test methods]
- ✅/❌ Feature Doc: [exists at path / MISSING]
- ✅/❌ GitHub Issue: [#N title / MISSING]
- ✅/❌ CHANGELOG: [updated / NOT UPDATED]
- ✅/❌ IoC: [registered / MISSING registrations]

Overall: COMPLETE / INCOMPLETE — [list what's missing]

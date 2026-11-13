# RCA: test-suite read caches, plan 046 (2026-11-13)

## Top-line

Plan 046 (#1132) caches the .cs file lists and text of Main, Client, Dependencies and CoopCompat in
`RepoPaths`, skips `ModuleData/Languages` in two NPCCharacter scans and compiles each shipped XSLT once
through `CompiledXslt`. Only files under `TAOM.Tests` changed. The data-flow lens traced 14 flows and found
no gap. It found two LOW inconsistencies, both confirmed by the review lead against the worktree and both
fixed in the review follow-up commit. No CRITICAL, HIGH or MEDIUM finding. Codex was not run (out of usage;
a paid call needs the maintainer's word).

The three questions the orchestrator asked all came back clean, and the lead spot-checked each answer:
no test writes a .cs file under the four cached roots (the lens read 559 write-call lines; the lead's grep
for a write call naming a root on the same line found none), no
converted read lost a normalisation, and no site calls `Load` or reads `OutputSettings` on a shared
`XslCompiledTransform` (15 `CompiledXslt.Get` sites, every `Load` is inside `CompiledXslt.Compile`).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `RepoPathsTests.cs` said of the `TAOM.Tests/bin/repopaths-text-probe.cs` probe that "only the root check keeps it out of the cache". The `bin` segment check in `IsCorpusFile` rejects it too, so neither clause had a test of its own: with the root clause turned into an `||`, all 19 RepoPaths tests stayed green | A test comment claiming an isolation the probe does not give | The probe went under `bin` so that the file it writes is git-ignored. That placement also trips the second clause, and the mutation proof the executor ran (`return true;` at the top of `IsCorpusFile`) breaks the whole method, not one clause | New test `Text_TrackedFileOutsideTheCorpusRoots_IsNotCached` reads a tracked .cs file outside the roots twice and asserts two string instances. Against the `||` mutant it was the only failure (`Failed: 1, Passed: 19`). The old probe's comment now says both checks reject it. Lesson in `lessons/testing-qa.md` |
| 2 | LOW | The run-snapshot warning ("a test that writes a .cs file under the roots must not read it through `Text`") was only on the private `CorpusRoots` field. The public `CSharpFiles`, `ReadSource` and `MentionsOutsideComments` docs, which callers read, did not say their results are now cached, and the class summary said "under the code roots", although `CodeRoots` is only Main and Client | Doc comment out of step with changed semantics | The change put the contract beside the data it guards, not on the entry points whose behaviour it changed | Fixed here: the class summary names the four roots and the snapshot effect, and each of the three public members says it is cached for those roots. One-off; no lesson |

## Why each agent missed these

- **Agent 5 (Data flow):** found both.
- **Other lenses:** the review lead received only the Agent 5 report for this item, so no other lens
  result is on record here. Neither finding breaks one of the audit's binding conditions (the root list,
  the obj and bin exclusion, the missing-file exception, the walk order, the untouched skip-list folders,
  the loader styles), which is what the orchestrator's spec check judged.
- **The executor:** wrote the probe comment from the intent of the test, then proved the test with a
  mutant that disables the whole method. A mutant per clause would have shown the gap.

## Feedback memories to codify

None. The durable rule is the entry in `docs/reviews/lessons/testing-qa.md`.

# RCA: plan 007 maintainer decisions, PatchShield seen/attached split (2026-09-24)

**Scope:** branch `improve/007-patchshield-skip-callback-shims`, diff `31a31f16..0bf2409e` (one
commit, `0bf2409e`, applying the four maintainer decisions). Reviewed by six `/deep-review` lenses
(standards, engine compatibility, efficiency, completeness, data flow, design) and a second Codex
adversarial pass (gpt-6-astra, 158,323 tokens). Report:
[deep-review-007-patchshield-skip-callback-shims-decisions-2026-09-24.md](deep-review-007-patchshield-skip-callback-shims-decisions-2026-09-24.md).
First-round RCA: [rca-patchshield-skip-callback-shims-2026-09-24.md](rca-patchshield-skip-callback-shims-2026-09-24.md).

## Summary

The code is correct: the seen/attached split keeps exactly the old dedupe set, so the same methods
get a finalizer, and every count is read under the lock. Every confirmed finding is text the
change made stale and did not sweep: a comment naming the renamed local, a folder class count in
two docs, a reword list for plan 006's merge that missed a line written on this very branch, and
test names and a message that claimed more than the test shows. Six findings confirmed (4 LOW,
1 INFO, 1 NIT), all fixed in the review follow-up commit. Two of them repeat lessons already on
file.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `PatchShieldPolicyTests.cs:264` comment still said `alreadyShielded`, the local renamed to `alreadySeen` in the same commit | Stale comment (rename) | The builder updated the call below the comment and the formatter's parameter name, then grepped for readers of the log text and `ShieldedCount`, not for the renamed local. **Repeat:** `lessons/build-tooling-workflow.md` "After a whole-word identifier rename sweep..." and "Renaming a test/symbol that docs reference by name" | New lesson: every rename ends with `git grep -n <old>` over the whole repo, comments included, before the commit |
| 2 | LOW | `ShieldCoverageTests` names broke `MethodName_StateUnderTest_ExpectedBehavior`; one test asserted an unstated false case, and its message claimed the failed-attach retry that only `PatchShield.Install` performs | Convention inconsistency; test claims more than it proves | The names were written as sentences about intent; the message was written from the production design, not the test body | One-off. Renamed, split, message narrowed |
| 3 | LOW | The "reword when plan 006 lands" list named `PatchShieldPolicy.cs:91-92` and the CHANGELOG but not `dr3-maintenance.md:261` (same claim, same branch), nor the older `dr3:304` and `harmony-il.md:572` | Incomplete cross-branch record | The list was written from memory of what the decision touched, not by grepping for the claim's words ("preserves the stack", "fallback paths") | New lesson: build a cross-branch reword list by grepping the claim |
| 4 | LOW | Adding `ShieldCoverage.cs` made `Dependencies/Foundation/` 19 types; `dr3-maintenance.md:248` and `feature-map.md:102` still said 18 | Stale inventory | The builder checked the docs the plan named (dr3's log sample), not the docs that count the folder. **Repeat:** `lessons/build-tooling-workflow.md` "When a change alters what an artifact CONTAINS, grep the docs for that artifact's inventory" | Extended in the same new lesson: adding a file to a folder a doc counts is an inventory change |
| 5 | INFO | The decisions-round Codex prompt was left untracked | Process | The prompt is written by the Codex dispatch, after the builder's commit; the first round committed its prompt with the report | Committed with the review record; the review lead commits the prompt with its report |
| 6 | NIT | `dr3-maintenance.md:287` ended with a colon, so the new-format sample read as the old conflated line | Doc accuracy | A history clause was appended to the sentence that introduced the sample | One-off |

## Root-cause pattern

Findings 1, 3 and 4 share one cause: **the builder's sweep looked for readers of what changed
(log parsers, `ShieldedCount` callers) and not for text that describes it.** The first-round RCA
found the same shape in claims ("every text described only the 3 shims"). A change's sweep has two
halves: code that consumes the change, and prose, comments and counts that state it. The second
half is the one this plan missed twice.

## Why each agent missed these

Every finding was caught in this round; the table says which lens did not, and why.

- **Agent 1 (standards):** caught 1, 2 and 3. Missed 4: its checks are ADR and naming rules over
  the changed files, not inventories in untouched docs.
- **Agent 2 (engine compatibility):** caught 1 as out-of-lens. Its scope is TaleWorlds APIs and
  engine claims; 2 to 4 are outside it.
- **Agent 3 (efficiency):** caught none, correctly: no finding has a performance component.
- **Agent 4 (completeness):** caught 1, 3, 4, 5, 6 and 2 (as a NIT for lens 1). The only lens that
  counted the folder at both commits.
- **Agent 5 (data flow):** caught 1 and 3 (and found the two older lines of 3). Missed 4: a class
  count is not a data flow.
- **Agent 6 (design):** caught 1. Its lens is simpler code, not doc drift.
- **Codex:** caught 2 (its P3). Missed 1, 3 and 4: it verified the new log strings against dr3's
  sample and the tests but read no doc outside the diff's files, and it did not re-read the
  comment above the updated call.
- **Builder self-check (the decisions commit):** grepped for readers of the old log format and of
  `ShieldedCount` (both clean) and stopped there.

## Feedback memories to codify

None. The pattern is already codified twice in `lessons/build-tooling-workflow.md`; the new entries
make the grep concrete for renames, added files and cross-branch reword lists.

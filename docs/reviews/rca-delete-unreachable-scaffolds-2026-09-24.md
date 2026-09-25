# RCA: plan 025, delete two unreachable scaffolds (2026-09-24)

## Top-line

Branch `improve/025-delete-unreachable-scaffolds`, diff `1091f3b6..032481cc`, reviewed by six
`/deep-review` lenses (standards, engine, efficiency, completeness, data flow, design) and one Codex
adversarial pass (gpt-6-astra, ultra). **No runtime defect was found**: the deleted types were never
resolved, injected or called, the 12 surviving registrations are intact, a config carrying the two
retired keys still loads, and the removed binding row covered no engine member. Seven findings were
confirmed, all LOW or P3 and all in docs, tests or the plan's prose; six are fixed on the branch
(report: `docs/reviews/deep-review-025-delete-unreachable-scaffolds-2026-09-24.md`), the seventh
is in the executed plan file and is recorded, not edited. The shared root: **the executor copied
the plan's prescribed prose, and the plan's history and engine claims had not been derived from
git or the engine.**

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | LOW | `editor-cache-rebuild.md:181`: after the rewrite, "That alone is a 2-3x win" followed the sentence about the deleted scaffold, and the premise ("memoize Phase 1's paths for Phase 2 reuse") is wrong for v1.5.3: Phase 1 reaches the engine only through `GetRealDistanceAndLandRatioBetweenSettlements`, which keeps its `NavigationPath` local and pathfinds with extra-cost multiplier 1, while `SandBoxNavigationCache.CheckBeingNeighbor` builds its own path with 2, once per direction | Wrong engine claim (doc) | The plan prescribed the new sentence and told the executor to keep the rest of the line unchanged; nobody re-read the neighbouring sentence or the engine | Paragraph rewritten from the v1.5.3 code, the unmeasured 2-3x claim dropped. Lesson in `misc.md` (plan prose is a draft) |
| F2 | LOW | `reflection-sites.md:81` said "Recover both from `6a80bac6`": the row came from `41258657` (`git log -S`), and restoring it to Category B would bring back the mislabel the same line describes | Wrong provenance (doc) | The plan's sentence named the scaffold's commit for both items | Line names `41258657` and routes any restored self-reflection to Category D. Lessons in `misc.md` and `adapters-taleworlds-api.md` |
| F3 | LOW | `editor-cache-rebuild.md:119` said the config provider has 20 tests; the change made it 22 (and `:122` said 8 smoke-gate tests where there are 6, pre-existing) | Stale count | The change dropped the uncomputed "103+" total but kept the per-bullet counts beside it | All per-bullet counts dropped. Recurrence note on the `misc.md` deletion lesson |
| F4 | LOW | `editor-cache-rebuild.md:113` still listed `NavigationPath` as a dependency; the deleted files were its last users in the feature (`git grep -w NavigationPath -- Main` now hits only `SupplyCaravanService.cs:854`) | Stale doc after deletion | The plan's doc sweep grepped for the deleted type names, not for types whose last user was deleted | Entry removed. Same recurrence note |
| F5 | LOW | CHANGELOG said the two keys were "never in the shipped `cache_rebuild_config.json`"; the config at `6a80bac6` carried both (lines 6-7) until `b5cb3018` the same day | Wrong history claim | Copied from the plan, which stated it without `git log` on the file | Reworded: carried only from `6a80bac6` to `b5cb3018`, before any release tag. Lesson in `misc.md` |
| F6 | LOW | `RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly` was a permanent test of absence: no behaviour, nine dead names kept greppable, and it sat in the config provider's class | Simplicity criterion (test) | TDD's RED step for a pure deletion was kept as a suite test; the plan prescribed it and allowed its deletion on review | Test removed (10288 to 10287); the compatibility test stays. Lesson in `testing-qa.md` |
| F7 | P3 | Plan 025 Step 6 prescribes a sentence containing `TaleWorlds.Engine.PathReuseCache` while its done check requires a whole-file grep for that text to find nothing; `:406` also says a citation ends "one line further off" when the deletion made it exact | Plan self-contradiction | The plan's checks were written apart from the prose they check | Recorded, not edited: the plan is executed and outside this change. Lesson in `misc.md` |

## Root-cause pattern

F1, F2, F5 and F7 all come from sentences the plan prescribed word for word, and F3 and F4 from the
plan's doc sweep, which searched for the deleted names rather than for facts the deletion changed.
The executor verified the code facts (every deleted name, every registration, the suite arithmetic)
and none of those were wrong. The errors sat in history and engine claims inside prose, which read
as verified because the plan was specific and cited commits.

This is a repeat of the #644 lesson in `lessons/misc.md` ("A change that deletes or moves data
invalidates numbers and line refs elsewhere"), third occurrence after #644 and #645, now with a
recurrence note naming the Dependencies list and hand-kept counts.

## Why each agent missed these

The lenses did not miss them; they reported them. What missed them was the build step:

- **Plan author (`/improve` sprint):** wrote the doc text from the audit summary; did not run
  `git log -S` on the binding row, `git log` on the config file, or read v1.5.3 `CheckBeingNeighbor`.
- **Executor:** followed the plan's "keep the rest of the line unchanged" literally, and its Step 8.4
  sweep whitelisted the absence test instead of asking whether it should exist.
- **Lens coverage:** all six lenses found F1 or its sentence problem; Agents 1, 3, 4, 5 and 6 found F3;
  Agents 2, 4 and 5 found F2; Agents 4 and 5 found F4; Agents 5 (N3) and 6 found F5; Agents 1, 4, 5
  and 6 raised F6. Only Codex found F7, because it read the plan's done checks against its prose.
- **Codex:** found none of F1 to F6. It read the doc sentence at line 181 as support for "Phase 2
  still pathfinds" and did not test the memoization premise, and it did not check the recovery
  commit or the JSON history.

## Feedback memories to codify

None beyond the three lessons (misc, testing-qa, adapters-taleworlds-api) and the recurrence note.
The binding-gate fallback (`ReflectionSiteBindingTests.cs:136-144`) is the one open mechanism; its
fix changes the gate's behaviour and is pre-existing test code, so it is a follow-up (plan 008's
area), not applied here.

## Not fixable on this branch

The body of commit `032481cc` also says the keys were "not in the shipped JSON" and that "another
[test] pins the types' absence". History is not rewritten (no amend or rebase on a reviewed hash);
the follow-up commit body and the CHANGELOG carry the correction.

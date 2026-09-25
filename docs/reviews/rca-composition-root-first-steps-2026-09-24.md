# RCA: feature-module composition root, plan 018 (2026-09-24)

## Summary

Plan 018 added `Main/Composition` (the module contract, `ModuleRunner`, the engine-facing hooks)
and moved WandererAllegiance into the first module. Six deep-review lenses and a Codex adversarial
review of `4c728dac..44045b34` produced 16 confirmed code and test findings (4 MED, 12 LOW), plus
a stale plan precondition and one pre-existing doc line, 4 items for Mike and 2 false positives.
The MED runner defect contradicted the change's own promise: a save-owning module that faulted in a fail-open step was skipped silently at the next campaign start, because
the "already faulted" skip ran before the fail-closed check, and the campaign's next save would
have dropped its data. A second MED item decides what that fix costs: TAOM's own
`Patch37_CrashReport` finalizer on `Module.OnApplicationTick` swallows the fail-closed throw while
crash capture is on, and the engine re-runs the loading step. Since the fix throws on every retry,
the load never finishes: the player is stuck on the loading screen, but no campaign runs without
the save owner. With crash capture off, the exception reaches the engine. That end state is traced
through the code, not seen in game; hang or silent data loss (or an inquiry and a return to the
menu) is Mike's decision. Nothing is live today: the only module
owns no save data. Report: `docs/reviews/deep-review-018-composition-root-first-steps-2026-09-24.md`.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | `ModuleRunner.Run` skipped a faulted module before its fail-closed rethrow, so a save owner that faulted earlier (or on a retried campaign start) was left out silently | Logic error | The two rules ("skip every later step", "save owners fail closed") were written and tested one at a time; the only campaign-start test passed `failClosed: false`, which production never does | Sequence tests (early fault then campaign start; failed start then retry); `RunCampaignStart` owns the flag; lesson in `lessons/state-lifecycle-save.md` |
| 2 | MED, latent | The fail-closed throw at campaign start is swallowed by TAOM's Patch37 finalizer and the engine re-runs the loading step | Missing vanilla gate (TAOM's own patch on the caller) | The plan traced the engine's call chain up from `OnGameStart` and stopped at "no managed catch"; nobody asked whether TAOM patches a frame on that chain | NEEDS MIKE; CHANGELOG known limitation; lesson in `lessons/state-lifecycle-save.md` |
| 3 | MED | `IoC.Modules = modules;` untested; its removal silences every module | Dead / no-op code (the "registered, invoked by nothing" class, again) | The kernel test pinned the two runner calls, not the hand-off between them; the hooks' null guard turns a missing hand-off into a silent return | Kernel test pins the line; mutation-checked |
| 4 | MED | The engine-facing hooks had no test | Missing test | The plan assumed the starters need a running game; v1.5.3 `CampaignGameStarter` and `BasicGameStarter` construct with no engine | Tested seam (overloads taking the runner and resolver); `FeatureModuleHooksTests` |
| 5 | LOW | Comments and CHANGELOG said modules run after every hand-wired block | Other: ordering claim from the plan table, not the file | The plan's ordering table missed four hand-wired blocks after the module call (the first fix still left out the co-op Harmony census after GameInit; the convergence pass added it) | Text corrected; OnGameStart branch position pinned |
| 6 | LOW | Campaign-start fail-closed flag lived outside the runner | Convention inconsistency | The flag was passed at the call site instead of named in the class that documents the rule | `RunCampaignStart` |
| 7 | LOW | `IoC.Resolver` invisible to the service-locator grep | Other: new container accessor | The review grep keys on the old spelling `IoC.Resolve<` | Source test allows only the hooks |
| 8 | LOW | Failed notice swallowed without a log line | Convention inconsistency | Plan 009's sibling logs it; the new catch copied the shape, not the log | Logged |
| 9 | LOW | Stale pilot test summary | Convention inconsistency | The migration deleted the lines the summary names | Rewritten |
| 10 | LOW | "six small types" (13 in six files) | Other: count from memory | The trade-off count was written, not computed | Recounted: five files, 11 types |
| 11 | LOW | `CustomBattle` comment omits the editor | Other: doc | The enum comment was written from the Custom Battle path only | Aligned with `RegisterCustomBattleModels` |
| 12 | LOW | A parked save owner failed closed in registration | Logic error | The rethrow condition ignored `State`, although the rule's reason (SyncData must run) never applies to a parked module | Parked modules never fail closed; test |
| 13 | LOW, latent | Slot test ignores hand-wired models | Missing test | The generic test was scoped to modules; the comment claimed more | Comment narrowed; guard deferred to the first model migration |
| 14 | LOW | Decl factories and guards untested | Missing test | Declared-but-unused dimensions were accepted as the plan's trade-off | `MissionBehaviorDecl.Of` covered; rest deferred |
| 15 | LOW | The `OwnsSaveData` IL check had never fired | Missing test (vacuous check) | The only declared behavior has an empty `SyncData`, so the check passed without checking | Positive control with `FieldCampCampaignBehavior`; lesson in `lessons/testing-qa.md` |
| 16 | LOW | The LF test passes without normalising on an LF checkout | Missing test (vacuous check) | The test's input needed no normalising on this working copy | Explicit CRLF probe; same lesson as 15 |

## Root-cause pattern

Findings 1 and 12 share one shape: two rules on the same state (skip a faulted module; throw for a
save owner) were each correct and each tested, and the code chose between them by the order of
two `if`s. The bug lived only in the sequence (fault in step A, then fail-closed step B), which no
test ran. Finding 2 is the same question one level up: the throw was traced through the engine
and not through TAOM's own Harmony finalizer on the frame above it.

Findings 3, 15 and 16 share a second shape: a check that cannot fail. A null-guarded hand-off, an
IL check over a set with no positive member, and a normalisation test fed already-normal input all
stay green when the thing they guard is removed. Each was caught only by asking "what would make
this test fail", then running that mutation.

## Why each agent missed these

- **Builder (plan executor):** executed plan 018 as written; the plan specified the "skip every
  later step" policy, the literal `failClosed: true` at the hook, the `failClosed: false` test and
  the LF-only reader test.
- **Agent 1 (Standards):** caught 5, 6, 7, 8, 9, 10. Runner semantics (1, 12) are outside its
  checklist.
- **Agent 2 (Engine compatibility):** caught 1 and 11 and proved the data loss in
  `CampaignBehaviorManager`. It traced the rethrow to "no managed catch" in the engine and did not
  look for TAOM's own finalizer (2).
- **Agent 3 (Efficiency):** no findings expected.
- **Agent 4 (Completeness):** caught 3, 4, 9, 10, 14, 15. It did not question the runner's order of
  checks.
- **Agent 5 (Data flow):** caught 1, 2, 5, 12, 13. It missed 3 (it traced the hand-off as
  CONNECTED rather than asking whether a test would notice its loss) and 15, 16.
- **Agent 6 (Design):** caught 1 (as P5) and 9.
- **Codex:** caught 1 (with the retry sequence) and 16. It missed 2 to 5, 12 and 15.

## Feedback memories to codify

Two lessons appended; no memory entry, since both are cross-feature rules for the lessons record:
- `docs/reviews/lessons/state-lifecycle-save.md`: a fail-closed rule is checked on every exit path,
  including the "already faulted" skip, and traced through TAOM's own finalizers.
- `docs/reviews/lessons/testing-qa.md`: a guard test needs an input that makes it fire.

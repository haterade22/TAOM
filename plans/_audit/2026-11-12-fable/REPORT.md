# REPORT: /improve run 2026-11-12-fable

## Summary

The maintainer asked for a repo-wide review that leads to refactors of code, processes, workflows and
documentation, with functionality kept, on a branch he merges later. Six read-only lanes audited the
baseline `7087fa633`; three refuters then tried to overturn the 27 findings that looked like plan
candidates. The refuters confirmed 5, narrowed 21 and refuted part of 1. Nine work items survive.

The repo is in better shape than its size suggests. The clone finder counted 878 duplicated lines in
4,217 production C# files. The performance lane found no code change worth a plan: in 23 profiled
battles the whole mission tick (vanilla and TAOM together) is 0.39 ms of an 8.69 ms frame. The doc
linter is clean except for five size warnings. So this run's work is a set of small, provable
clean-ups, not a rewrite.

The Kimi run of 2026-11-07 did one refactor (`e99fcccc9`, the character-creation bonus resolver) and
its docs. Nothing here repeats it. Its four open leads were given to the lanes: lead 2 (RefugeService)
became plan 049, lead 3 (logger resolves) is refuted as not hot, leads 1 and 4 stay watch items.

## Baseline

See [baseline.md](baseline.md). C# suite `Passed: 39250, Failed: 0, Skipped: 9`; hook suite `1267
passed, 0 failed`; ModuleData `0 error(s)`; doc lint exit 0. **The Python suite is red on the trunk
before this run**: `Ran 8578 tests`, `FAILED (failures=7, errors=2)`, all heraldry and kingdom
generator checks against shipped data.

## Reconcile of the last backlog

Plans 001 to 042 are merged. 043 (#739) and 044 (#1038) are still TODO and wait on the maintainer
(044's Step 0, a `/verify` row for the no-game unit step, is DECISIONS row 4 of the 2026-10-28 run).
045 (the hair engine) is complete by its own status line.

## The work items, by leverage

Each row is the refuters' recalibrated view, not the lane's.

| # | Item | From | Effort | Risk | Who |
|---|---|---|---|---|---|
| 046 | Test suite reads the C# corpus once: cache code-root text in `RepoPaths`, skip `Languages/` in two character scans, compile each shipped XSLT once. Measure first; stop under the threshold. | TST-01, 03, 04 | M | LOW | executor |
| 047 | Fold three safe twins: one rules helper for the LotrIssue templates (four copies of the frequency map), one CultureStageView type probe for three patches, one supply row VM | ARCH-02, 06, 05 | S to M | LOW | executor |
| 048 | Delete eight members nothing calls, and correct two stale comments | ARCH-07, DOC-01 | S | LOW | executor |
| 049 | Extract RefugeService's save-load repair into one unit, with lazy clock seams | ARCH-03 | S | LOW | executor |
| 050 | Move the messenger arrival verdict into MessengerService as one tested classification | ARCH-01 | M | MED | executor |
| 051 | Tools: delete three dead scripts, correct the catalog's environment section, share one doc-graph build in its tests, hoist the sibling parse in the translator's coverage test | TOOLS-01 (part), 08, 03 (part), 04 | S | LOW | executor |
| 052 | Harness text: remove two duplicated passages from CLAUDE.md, raise `/verify`'s discovery floors and drop its stale counts, make `/review-codex` 3f run filtered tests per fix and the full suite once | DX-03 (1, 2), DX-09, DX-02 | S | LOW | orchestrator |
| 053 | Hooks: parse the edited path in bash in the polearm hook; cut session-start lines that repeat the harness's own context | DX-05, DX-06 (1, 2) | S | LOW | orchestrator |
| 054 | Docs: correct wrong statements an author would act on, add the Realms row and 11 doc links to the feature map, add a search hint to three navigation maps, mark answered handbook notes | DOC-01, 02, 03, 04 | S | LOW | orchestrator |

Items 052 to 054 are text and bash edits with exact lines from the refuters. The orchestrator makes
them directly: a plan, a cold review and an executor would cost more than the edits. They get the
same gates (doc lint, `reviewctl.py lint`, the hook suite, `/security-scan`).

## Lanes

- **arch** ([lane-arch.findings.md](lane-arch.findings.md), [verify-arch.md](verify-arch.md)): 10
  findings. The refuter caught two traps a plan must carry: an eager clock read would break two
  RefugeService tests, and the messenger arrival rule drops prisoners as lost although its comment says
  wait.
- **perf** ([lane-perf.findings.md](lane-perf.findings.md)): 5 findings, none a refactor. The top item
  is a measurement only the maintainer can take.
- **tests** ([lane-tests.findings.md](lane-tests.findings.md), [verify-tests-tools.md](verify-tests-tools.md)):
  8 findings. About 45 to 50 whole-tree source reads per run; the Python floor is 13 s of the 112 s
  suite.
- **tools** ([lane-tools.findings.md](lane-tools.findings.md)): 10 findings. The refuter shrank two of
  them: of 25 "finished one-offs" only 2 meet the catalog's own move rule, and two of the five
  "superseded" validators are live steps in about 25 doc lines.
- **harness** ([lane-harness.findings.md](lane-harness.findings.md), [verify-harness-docs.md](verify-harness-docs.md)):
  11 findings. Both always-on size caps have 10 to 14 bytes left.
- **docs** ([lane-docs.findings.md](lane-docs.findings.md)): 9 findings.

## Bug leads (behaviour changes, so no refactor plan)

- **Messenger to a prisoner is lost on arrival.** `MessengerCampaignBehavior.cs:283-287` drops every
  target whose `HeroState` is not `Active` (or `NotSpawned` for a wanderer), so the prisoner wait at
  `:622` is unreachable, although the comment at `:304-305` says the player may resolve it. An inactive
  wanderer who is `Released` or `Traveling` can be sent a messenger that is then always lost.
  ([verify-arch.md](verify-arch.md), ARCH-01.)
- **The Fords match allocates about 92 KB per frame**, nine times every other battle in the November
  logs (PERF-02). Skip-list area; taom-b4 was told.
- **The custom battle muster VM has three commands no prefab binds** (`ExecuteUseOldScreen`,
  `ExecuteClosePalette`, `ExecuteCloseIcons`; ARCH-08). It landed today, so it may be work in flight.
- **The Python suite is red on the trunk** (TOOLS-10; baseline.md).

## Valid but unplanned

- TST-02 (IL call index): measure `TaomAssemblies.FindCallers` twice in one process first; drop it if
  the second walk is under 0.5 s.
- TST-05: 1,056 source-text pins in 205 test files raise the cost of every extraction. No bulk rewrite;
  the pins that read `SubModule.cs` and `IoC.cs` (163 sites) block the composition migration.
- TST-06 and TST-07: one repo-root locator, one string-row reader, one safe `GetTypes`; 47 repo-data
  checks that skip instead of failing. Mechanical, many files.
- TOOLS-05 (31 scripts hardcode the install path), TOOLS-06 (one byte-faithful XML helper), TOOLS-07
  (per-test `git init` fixtures, about 100 s of the suite; measure one module first).
- TOOLS-02, reduced: `cleanup_deleted_gondor_armor.py` and `cleanup_deleted_gondor_items.py` fit the
  catalog's move rule; each needs its repo-root arithmetic fixed on the move.
- ARCH-04 (one single-charge tree for war ram, elk and Animalia): needs an in-game ride as proof.
- ARCH-09 (Messengers hand wiring into its module): single-owner files, and it changes behaviour order.
- ARCH-10 (FamilyTreeLayer onto the generic popup layer): needs both popups opened in game.
- DX-10 (one git call in the Stop hook): wait for harness-retro proposal P1.
- DOC-08 (four culture docs overlap): 054 fixes the wrong lines; the restructure is a larger edit.
- PERF-03 (the 1.5 s native action-set merge at launch): measure with the plan 042 harness first.
- PERF-04 (three small allocations per frame in the custom-battle horn call): low leverage.

## Decisions for the maintainer

See [FOR-MIKE.md](FOR-MIKE.md).

## Considered and rejected

- Kimi lead 3, `IoC.Resolve` in `LogTask.Execute` and `TaomBTLogger.LogMessage`: the first runs at
  most once a second for one agent, the second only on a fault (lane-perf).
- The 130 single-implementation service interfaces: ADR-002's 2026-09-24 amendment says they go one at
  a time when their file is next touched.
- An assembly-level `[Parallelize]`: 32 test files assign process-wide statics.
- A substring prefilter in `test_ingame_verdict.py`: the refuter measured it slower than the lexer.
- Moving `docs/reviews` into an archive: 1,052 outside references, 28 of them in `.cs` comments.
- A single dispatcher for the Bash gates: rejected in September; the prefilters hold.
- A shared argparse helper for `--apply` and `--dry-run`: the semantics differ per script.

## Not audited

Correctness and security in code (playbook 1 and 2), game data (9), direction (10), dependencies and
migrations (6), and the skip-list areas in BRIEF.md.

## Where the evidence is

This folder: one `lane-*.findings.md` per lane, one `verify-*.md` per refuter. The agents' scratch
scripts and timings are outside the repo, in the run's scratch folder.

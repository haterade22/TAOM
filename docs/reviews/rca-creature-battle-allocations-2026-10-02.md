# RCA: plan 033 review findings (creature-battle allocations and scans)

**Date:** 2026-10-02 (review run 2026-10-03)
**Scope:** the `/deep-review` of plan 033, `d50bf962..d45774a5` on `perf/033-creature-battle-allocations`
(six lens reports, no Codex). Report: `docs/reviews/deep-review-033-creature-battle-allocations-2026-10-02.md`.

## Top-line

Plan 033's four commits keep every creature's behaviour: the clock, reuse and grid changes are correct, and the
lenses found no runtime defect in them. The review's main finding is about a cost claim. Commit 4 (`d45774a5`) made
the spider hunt ask the engine's nearby query first, to replace "up to three native reads per hostile agent". On
v1.5.3 those reads are inlined pointer loads (`AgentHelper`, `[AggressiveInlining]`), while the query walks the same
fields for every agent slot under a static lock and adds a locked managed lookup per result. The change cost more
before contact by its own account and was never measured, so it was reverted to the base scan, which picks the same
target. The other confirmed findings are a process-lifetime buffer that kept agents alive, a skip ledger formatted
inside an entry point, and four tests or rules that passed a mutation their own claim covers.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| R1 | MEDIUM | Commit 4's win rests on "native reads" that are inlined pointer loads; the replacement costs at least as much and more before contact | Other: cost premise from API names | The plan counted calls by name (`IsHuman`, `IsActive()`, `Position`) and verified the query's equivalence in native code in depth, but never opened the three getters it was replacing. A repeat of the 2026-08-03 `GetRaceNames` lesson: cost is a decompile question | Reverted. Lesson: a perf commit's win names the cost it removes, read from both sides' bodies (`lessons/adapters-taleworlds-api.md`) |
| R2 | MEDIUM | The skip ledger (counters plus two formatted log lines) grew the entry point from 397 to 512 lines, and its format pins sat only in a `RequiresGame` class | Convention inconsistency | The plan placed the D6 lines where the callbacks were; the executor followed it. The "pure rule's tests go in an untagged class" lesson (plan 026) was not in the plan's test design | `CallbackSkipLedger` plus untagged `CallbackSkipLedgerTests`. Recurrence noted in `lessons/testing-qa.md` |
| R7 | LOW | `CreatureTreeClockTests` covered five namespaces while its commit claimed every creature-tree node | Other: gate scope narrower than its claim | The prefixes listed the folders the commit edited, not the folders holding tree nodes; the control test proves the scanner sees a read, not that the filter covers the claim | Eight prefixes; mutation proof in two new namespaces. Lesson in `lessons/testing-qa.md` |
| R8 | MEDIUM | 10 of 14 early-return gates untested; the two self values TAOM uses not proven to reach a listener | Other: test gap on a new gate | Tests were written for the allocation win (no listener) and one global delivery, not for each gate's value list; three lenses checked the lists by eye | Self-value delivery tests and a table over every callback and value, each proven red by a mutated gate. Same lesson |
| R9 | LOW | `SpatialGridDebugService._nearby` (IoC singleton) stayed full after each frame, keeping a finished mission reachable | Stale state / lifecycle | Turning a local into a field changed its lifetime to the owner's; nobody asked what the owner's lifetime was (`Reuse.Singleton`) | `_nearby.Clear()` after the loop, IL test red first. Lesson in `lessons/state-lifecycle-save.md` |
| R10 | LOW | `SelectorListReuseTests` checked identity only; deleting both `Clear()` calls stayed green | Other: test gap | The test asserted the optimisation (same instance), not the behaviour it must preserve (same contents) | Content assertions, proven red with the clears removed. Same testing lesson |
| R11 | LOW | The two-map swap over one spare stack had no content test | Other: test gap | The reuse tests drove one map; the production pairing lives in a private method on Agent types | A pure six-round two-map test, proven red with `cells.Clear()` removed |
| R12 | LOW | Two docs still named `DateTime.Now` | Stale claim after a change | The executor updated the docs the plan named; a grep for the old identifier across `docs/` was not in the plan | Fixed; covered by the existing docs-sweep lesson (`lessons/build-tooling-workflow.md`, "When a finding reverses a documented claim, grep the claim's key terms") |
| R13 | LOW | `warg-combat.md` "Grid updates: Every 5 ticks" (stale before the branch) and a stray blank line | Stale claim after a change | Same as R12: the grid's cadence changed and the warg doc's line was not grepped | Fixed |

## Root-cause pattern

R7, R8, R10 and R11 share one shape: the test was written to show the change works, and nobody asked which
mutation inside the change's own claim would leave it green. Each was caught by a lens reasoning about a deleted line
("if both `.Clear()` lines go, the test still passes"). The fix this time was to run that mutation for every new or
widened test and keep the red log (`scratch/lead-033/red-*.log`, outside the repo).

R1 is the same shape at the design level: the plan proved the new path correct with great care and never measured,
or even read, the old path it claimed to beat.

## Why each agent missed these

The lenses did not miss them; these are the lenses' own findings. What the build missed:

- **The plan and executor (R1):** the plan's "Change E" counted native reads by method name. The native evidence it
  gathered (four decompiles) was all on the new side.
- **The executor's tests (R7, R8, R10, R11):** each test passed its RED step against the base, which proves the
  change happened, not that the test would notice the change being undone later.
- **Agent 6 dissented on R2** (a one-caller class); kept as recorded, outweighed by CI coverage of the pinned lines.
- **No lens missed R9:** five found it independently.

## Feedback memories to codify

None beyond the three lessons appended: the cost-premise rule already exists and gains a recurrence entry, and the
mutation rule is new in `lessons/testing-qa.md`.

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `d45774a5..7021101b` | C1: the `childrenWithTasks` content assertion R10 added could never fail, because the test tree's only selector child is an undecorated task, so `Prepare` never adds to that list | `066e129f` | R10's proof deleted both `Clear()` lines at once; the executable-children assertion went red and hid that the other assertion could not | Mutate each line a test claims to guard on its own, one run per line (the `lessons/testing-qa.md` mutation rule, applied per line) |
| 1 | same | C2: the widened clock rule missed `CreatureBanditBehaviorTree` and `TrollBruteForceBehaviorTree`, the two tree classes outside a `.BehaviorTreeElements.` namespace, while the `7021101b` body and the REVIEW-LOG entry claimed every creature-tree namespace | `066e129f` | R7's fix chose its prefixes from a namespace pattern, not from the list of classes deriving from `BehaviorTree` | When a rule claims "every X", list X from the code (here, every class deriving from `BehaviorTree` under `Main/`) and check the rule's scope against that list |
| 2 | `7021101b..066e129f` | none (clean) | | | |

This section and the REVIEW-LOG entry's convergence clause were written by the orchestrator after round 2,
as for plan 030: the review workflow's fix pass records a round only in the report.

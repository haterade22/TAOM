# RCA: load-time stamps review follow-ups (plan 040, 2026-10-02)

## Top line

Plan 040 adds three kinds of load-time stamps to `taom_debug.log`: always-on patch-phase totals and
per-type `[LoadXml]` lines, and, behind "Enable Load-Time Stamps", per-category, per-hook and
per-campaign-handler lines. A six-lens review found nothing CRITICAL, HIGH or MEDIUM. Every
confirmed finding is about how true the stamp's own report is: what a number measures, what a
WARNING says, and what a doc claims. None changes game behaviour. The worst: the `[Lifecycle]`
dispatch time contained the stamp's own listener swap and log writes, while three texts said the
remainder was the engine's receivers; and the once-per-process "per-handler timing off" WARNING was
written by faults that turn nothing off, and its shared latch could then hide a real switch-off.
Everything confirmed was fixed before the commit, red-first where testable; the GitHub issue (D4) and
four design questions go to Mike.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | LOW | The C3 dispatch `ms` started before the reflection swap and was read after every C1 and C2 line was written synchronously; the feature doc, the registry and the stage C commit body said `ms - listeners_ms` was the IssueManager and QuestManager receivers (only QuestManager overrides any of the five in v1.5.3) | Measurement window | The parallel `[LoadXml]` path reads its end tick before logging, but the lifecycle path was written as "log, then total"; the doc sentence was written from the design, not from the code's clock reads | Start after the swap, end tick first, pinned by a test whose logger and adapter advance the clock; lesson in `build-tooling-workflow.md` |
| 2 | LOW | Begin's gate or clock fault, End's fault and a restore fault all wrote C4 ("per-handler timing off for this session; dispatch totals are still written") without turning anything off, and their shared latch silenced a later genuine wrap failure | Reason-line truth | D6 was checked for the presence of once-only reason lines, not for each path that writes one | A separate C5 stamp-fault line with its own latch; four tests; lesson in `build-tooling-workflow.md` |
| 3 | LOW | `LoadStampDetailGate` returned false on a throwing toggle read with no line, though its summary promised a log always says why the detail is missing | Silent fallback | The catch was written as "fail safe", and fail-safe was read as "done"; the test asserted only the return value | H3 WARNING when the failure turns the detail off; lesson in `build-tooling-workflow.md` |
| 4 | LOW | H1 and the configuration row said the toggle is read live with no restart, but the per-category lines are decided once per process at the first game initialization | Doc versus guard | The once-per-process guard sits in `SubModule.cs`, away from the text that described the toggle | Text fixed; same lesson as row 6 |
| 5 | LOW | The `reflection-sites.md` rows were appended after the table's trailing blank line, and generic-arity names broke their code spans | Doc rendering | Appending to a table by line number past its blank line; no render check | Rows joined, double-backtick spans; covered by the existing doc-hygiene lesson set, one-off |
| 6 | LOW | The `feature-map.md` row kept describing stage A after stages B and C landed | Staged doc drift | The plan's stage B and C steps did not list the row, and each executor followed its stage's steps | Row updated; lesson in `misc.md` |
| 7 | LOW | "Not timed" omitted a save load's `OnGameLoadFinished` fan-out, against the doc's "every campaign handler of a loaded save" | Coverage claim | The dispatch set was taken from `Campaign`, and `OnGameLoadFinished` is dispatched from `SandBoxGameManager` | Listed under "Not timed"; same lesson as row 6 (re-read a coverage claim against every dispatcher) |
| 8 | LOW | The caution covered only the toggle-on wrapper frame; the seven always-on targets put a TAOM-patched frame in every load-time crash stack | Side effect undocumented | The always-on patches were judged on CPU cost only | Caution and registry sentence |
| 9 | INFO | X1 `result=ok` read as "the type loaded" | Doc precision | Field meanings were written from TAOM's side of the call | Row reworded |
| 10 | LOW | No Performance section; the always-on cost was not stated | Template gap | The doc was written section by section from the plan, which had no such section | Section added |
| 11 | NIT | A failed per-call `[LoadXml]` write dropped the call from the summary | Ordering | The aggregate was added after the log call | Aggregate first; test |
| 12 | NIT | `StopwatchStampClock.Instance`'s summary gave a wrong reason; an unused `using`; "How to read" left out where the `OnGameStart` lines land; no C0 binding-missing example | Comment accuracy | Small, unreviewed by the executor's own pass | Fixed |
| 13 | LOW | Test gaps: HookTimer's never-throws contract, the module's registrations, the merge finalizer behind a skipping prefix | Test coverage | Each contract was argued in prose (a comment or the doc), so it read as covered | Three tests, each proven to fail against a mutated copy; the adapter's two branches stay a follow-up |

## Root-cause pattern

Rows 1 to 4 share one theme: an instrument's own report was reviewed for presence (a line exists,
a total exists, a reason line exists) and not for truth (what the window contains, what each writer of
a line actually did, what each fallback says). Rows 4, 6 and 7 share a second: a claim written once
(the toggle is live; the row describes the feature; every handler is timed) was not re-checked when a
later stage or a distant guard changed what it covers.

## Why each agent missed these

The lenses found every row; the question is why the executor's own pass and the plan did not.

- **Executor (plan steps):** the plan's D6 checklist asks for once-only reason lines and literal
  format pins, which the code has; it does not ask what each writer of a reason line did, nor where
  the clock reads sit against the stamp's own logging.
- **Agent 1 (Standards)** checks ADR and naming rules; measurement and reason-line truth are outside
  its checklist, which is why it reached rows 1 to 3 only through the orchestrator's focus probes.
- **Agent 2 (Engine)** found row 1 as a WRONG engine claim (which receivers exist), the angle its
  checklist gives it.
- **Agent 3 (Efficiency)** found row 1 from the cost side; row 2 only as a deviation check.
- **Agent 4 (Completeness)** found rows 10 and 13, the template and test-home gaps its checklist names.
- **Agent 5 (Data flow)** found rows 4, 7 and 8, which only a trace of the toggle and the dispatch set
  reveals.
- **Agent 6 (Design)** found row 2 as a design proposal; its fix changed behaviour, so the
  behaviour-preserving separate latch was applied instead.

## Feedback memories to codify

The three lessons below go to `docs/reviews/lessons/build-tooling-workflow.md` (rows 1 to 3) and
`docs/reviews/lessons/misc.md` (rows 4, 6 and 7). No new rule file: the D6 logging note already
requires reason lines; the lessons sharpen how a reviewer checks them.

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `a07546a7..f1267a98` | The dispatch start tick was taken inside the wrap try, so failure paths timed the stamp's own work | `e6dcb83f` | The fix for finding 1 was tested on the success path only | Test every exit path of a timed window |
| 1 | same | A restore fault reported "lines may be missing" while what it leaves is a wrapper in the record | `e6dcb83f` (new C6 line) | The reason line was routed to the nearest existing writer | A reason line states what its writer's failure actually leaves (D6) |
| 1 | same | The detail gate stored only the value, so a recovered off read after H3 logged nothing | `e6dcb83f` | Equality on the value, not on what was last logged | Remember the logged state, not the value |
| 1 | same | Three doc and registry texts (a dispatch count, a line order, the crash-frame note) were wrong or incomplete | `e6dcb83f` | Each was written from the plan's text, not the engine | Read counts and orders from the decompile |
| 2 | `f1267a98..e6dcb83f` | R2-1: a test kept its name after round 1 changed which fault its setup produced, leaving the C5 latch unpinned | the orchestrator's records commit | The latch split moved the test's fault without anyone re-reading the test | When a fix re-routes a fault, re-read every test that produces it |
| 2 | same | R2-2: three comments still described the pre-round-1 behaviour | the same commit | The fix updated docs and tests, not the comments beside the changed code | A behaviour change sweeps the comments of the members it changes |

This section and the REVIEW-LOG entry's convergence clause were written by the orchestrator after round 2.

# RCA: hitch probe and profiler extensions review findings (plan 041, 2026-10-02)

## Top line

Plan 041 (`56b2b327`) turns a hitch probe on for every player: Patch98 brackets five engine methods,
feeds plan 028's profiler, and adds spawn, script and clip-loading lines. No engine incompatibility was
found, and no hook can throw into the engine. The six lenses found four MEDIUM findings and a LOW tail,
all in what the probe and the older profiler say about each other once both exist: an installer catch
that broke the invariant the new measuring formula relies on, plan 028's installer tests passing
through that catch on a swallowed assertion, plan 028's "nothing is measured" reason lines false in the
default configuration, and the moved per-mission logic left untested. Four findings repeat review 028
lessons written the same day. Every confirmed finding is fixed in this review's commit, test first
where testable; two native questions (the clip walk's real cost and its safety under concurrent
loading) remain UNVERIFIED and go to Mike.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| R1 | MED | `HitchProbeInstaller.Install`'s catch left `MissionTickProfilerHooks.Installed` true, so full-mode timing could run with no frame boundary | Stale state across a failure path | The fold `Installed = Installed && ProbeInstalled` was written on the success path; the catch was copied from the probe's own flag only | The catch clears both flags; a fault-path test drives a throwing apply (`Install_ProbeInstallThrows_...`) |
| R2 | MED | Plan 028's installer fake asserted the category inside a delegate production wraps in a catch, so two tests passed on the error path | Test that cannot fail | The executor ran plan 028's tests green and read green as "still valid"; nobody asked what the fake does with the new second category | The fake answers both categories, the success test asserts no ERROR; lesson in `lessons/testing-qa.md` |
| R3 | MED | Plan 028's not-timing reason lines said "nothing is measured" while the probe measured; the restart choice blamed an install that never ran | Reason-line accuracy (D6) | The plan limited `TickProfileLines.cs` to one line, so the executor fixed `OffLine` and left its siblings, whose claim the same change made false | Probe-aware lines chosen by `_measuring`, all pinned; lesson in `lessons/misc.md` |
| R4 | MED | The logic moved into `MissionTickProfilerHooks.Probe.cs` and the default-on mission flow had no test | Test coverage, a repeat of review 028's R4 | Moved to stay under ADR-002's line limit; the Not-tested trailer kept the old "needs a live Mission" reason, which no longer applied to the moved code and had already been disproved for plan 028 | 15 tests, nine RED first; lesson in `lessons/testing-qa.md` |
| R5 | LOW | Header said `script attribution on` with the OnTick delegate unbound | Logic error | The plan's formula compared the site count with an expected count that already shrinks when the delegate is unbound | The delegate is a term of the flag; RED test |
| R6 | LOW | One attribution fault turned all attribution off, the line named one part, later missions kept the flags | Reason-line accuracy; stale state | The latch was private to the helpers, so the per-mission configuration could not see it | `MissionAttributionHooks.Off` folded into the flags, a line naming every part; RED test |
| R7 | LOW | A pre-tick fault left clip sampling claimed on, writing `animLoading=0` | Stale state across a failure path | The sample lives inside the pre-tick bracket, but the fault handler only knew about the bracket's own timing | The catch drops the sampler and the flag; RED test |
| R8 | LOW | `[TickSummaryExtra]` after "no mission summary" | Contradictory lines | The extra summary was written beside the summary call, not beside its frames check | Same frames check; RED test |
| R9 | LOW | Probe switched off in session: no reason line | Reason-line coverage (D6) | The early return assumed the game-start `probe off:` line explains every off case | One INFO line in the uncovered case; RED test |
| R10 | LOW | `ProbeNotInstalledLine` blamed only a failed install | Reason-line accuracy | Written for the failure case, not for "was never installed" | Both causes named; RED test |
| R11 | LOW | Hints and allowlist said a change needs a restart; "every battle frame"; "a few microseconds" | Stale description, a repeat of review 028's R11 | The probe's hint was written from the install, not from the per-mission re-read the same plan added | Both directions stated; the cost sentence points to the logged figure |
| R12 | LOW | "No shield finalizer inside the measurement" while `SpawnAgent`'s callee `Agent.EquipItemsFromSpawnEquipment` is shielded | Unverified cost claim, a repeat of review 028's R3 | The claim was written from the five bracketed methods, not from what they call | Reworded in four places; lesson in `lessons/harmony-il.md` |
| R13 | LOW | Doc drift (failed Patch98, "exactly once", thread wording, cost scope) | Stale description | The docs were edited for the new mechanism, not swept for the old sentences it changed | Reworded |
| R14 | LOW | A by-name lookup with no reflection-site row; a stale row location | Gate coverage, a repeat of review 028's R13 | The functional binding test was taken to cover the catalogue | Row added, locations corrected (gate 412) |
| R15 | LOW | The benchmark baseline already measured the agent-tick pair, so its new cost cancelled out | Measurement scope | The baseline was set up in the patched arm's state, not in the state players had before the change | Baseline with no profiler; lesson in `lessons/misc.md` |
| R16 | LOW | A fixed 40 ms bound over two `Sleep(10)` calls on a hosted runner | Flaky test | Written against the desktop's timer | Bound by the test's own stopwatch |
| R17 | LOW | A probe fault's time redistribution undocumented | Doc coverage | The fault line was written per part, the columns per mode | Feature doc row |
| R18 | LOW | Patch98's class order mattered and was unpinned | Implicit ordering | The order was right by accident of declaration | Comment and a source-order test |
| R19 | LOW | `ProbeTotals.cs` named for no type and holding an unrelated binder | Naming | The plan's file layout was followed literally | `ProbeLineData.cs`, `ProbeDelegates.cs` |

## Root-cause pattern

Most findings share one cause: **a second instrument changed what the first one's lines and tests
mean, and the change was checked only where it was edited.** The probe made plan 028's "nothing is
measured" lines false (R3), its second category made plan 028's installer fake throw inside a catch
(R2), its catch broke an invariant plan 028's flag rested on (R1), and its new fault latches left plan
028's per-mission flags claiming work that no longer happened (R6, R7). The plan froze plan 028's files
to one line each, which kept the diff small and left every consequence in those files unexamined.

The second pattern is **repeat findings from a review finished the same day** (R4, R11, R12, R14 repeat
review 028's R4, R11, R3 and R13). Plan 041 was written before review 028's lessons existed, and its
executor worked from the plan; the lessons were in the repository but not in the plan's checklist.

## Why each agent missed these

The lenses found every finding here; this section records which lens did not, and why.

- **Standards (L1)** found R1 to R6, R11, R12 and R19, and missed R7 to R10: its checklist reads each
  changed method's own contract, and those four are cross-method state (a fault in one bracket, a flag
  set in another).
- **Engine compatibility (L2)** found R1, R13, R14 and R18; its scope is API and IL facts, so the
  reason-line and test findings were outside it.
- **Efficiency (L3)** found R1 and R15; it judged the always-on path and the benchmark, not the status
  lines.
- **Completeness (L4)** found R1 to R4, R11, R13, R14 and R16; it checks tests and docs against the plan,
  so line-level D6 accuracy beyond the restart lines was not its checklist.
- **Data flow (L5)** found the widest set (R1, R3, R4, R6 to R11, R17); it missed R2's mechanism, which is
  a test-side flow, and R12, which needs the engine's call graph under `SpawnAgent`.
- **Design (L6)** found R1, R5 and R12's comment half (D9), and proposed the deletions applied in Step 4;
  it reads structure, so the reason-line contradictions were not in its frame.

## Feedback memories to codify

- When a change adds a second measurer, sweep every reason line of the first for a consequence the
  second makes false (`lessons/misc.md`).
- A fake that asserts inside a delegate production calls from a catch proves nothing on the success path
  (`lessons/testing-qa.md`).
- Logic moved to meet a line limit is testable code the moment it leaves its untestable host: test it in
  the same change (`lessons/testing-qa.md`, a recurrence of the plan 028 lesson).
- "No shield finalizer inside the bracket" covers the bracketed methods only (`lessons/harmony-il.md`,
  a recurrence of the plan 028 lesson).
- A benchmark's baseline arm runs in the state players had before the change, and a cost claim names
  what the benchmark stubbed (`lessons/misc.md`).

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `56b2b327..0d030997` | C1: R11's new "Enable Hitch Probe" hint and `mcm.md` said turning the probe off stops measuring, false while the tick profiler is on (its full mode keeps every probe bracket) | `2ab27cdd` | R11 was reworded from the probe's own gate, not from the combined `_measuring` formula both toggles feed | A toggle's hint is written from every condition that reads it (`git grep` the setting), not from the one path being fixed |
| 1 | same | C2: the `ProfilerOffAtGameStart` summary still described the flag's pre-R3 meaning | `2ab27cdd` | R3 changed the flag's consumer and left its declaration's comment | A fix that changes what a flag means rewrites the flag's summary in the same edit |
| 1 | same | C3: three test names kept identifiers that D7 and D8 renamed | `2ab27cdd` | The renames swept production identifiers and test bodies, not test method names | A rename's sweep greps the old identifier everywhere, test names included |
| 2 | `0d030997..2ab27cdd` | R2-1: C2's rewrite overstated: the flag is true only when the probe was on and the profiler off, since `Install` returns first with both off | the orchestrator's records commit after `2ab27cdd` | The rewrite was checked against the assignment line, not against the early return above it | A comment that states when a flag is set is checked against every return before the assignment |

This section and the REVIEW-LOG entry's convergence clause were written by the orchestrator after round 2:
the review workflow's fix pass records a round only in the report.

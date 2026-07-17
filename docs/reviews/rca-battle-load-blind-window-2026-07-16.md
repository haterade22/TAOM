# RCA — battle-load blind window + FileLogger durability (2026-07-16)

**Scope:** commit `c53c8436` (`fix(battle-load): durable logs + split load blind window (#350)`) — FileLogger crash-durable sync drain + 4 new Patch43 stamps splitting the `MissionState.OpenNew` → `Mission.Initialize` window. Review: 6 agents (5 core + 1 dedicated concurrency agent).

**Result: 2 confirmed defects, both in `FileLogger.Drain()`, both introduced BY the fix itself.** Standards, API compatibility, completeness, and data flow all passed. The two defects were found only by the non-standard 6th agent; **all five core agents read the same file and missed both.**

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | `Drain()` early-returns on `_logFile == null` **before** dequeuing. Post-`Dispose`, items stay queued; `ProcessQueue` loops while `!_queue.IsEmpty`, so a still-live writer thread (`Join(5s)` timed out, or an enqueue raced the null) spins at 100% of a core until process exit. **A regression:** the old loop always `TryDequeue`d and wrote via `_logFile?.`, so it could not spin. | Concurrency / liveness | No TAOM review agent has a thread-safety rule set. The 5 core agents check allocations, LINQ, hot paths, IoC — none reason about loop-exit conditions or liveness. The rewrite preserved *ordering* and *null-safety* (both tested) and silently dropped the *drain-regardless* property, which nothing named or tested. | Added `Dispose_ThenManyWrites_QueueStillDrains_SoWriterCannotSpin` pinning the invariant. Lesson filed (below) — **when a rewrite adds an early return to a loop body, enumerate what the old unconditional path guaranteed.** |
| 2 | MED | `Drain()`'s `catch` was empty, so a durable write fault (disk full, AV lock, dead handle) silently dropped the in-flight line — and every subsequent drain — forever, with zero signal. The crash-forensics instrument would look healthy while losing exactly the lines it exists to capture. | Diagnostics honesty | The empty catch was *deliberate and reviewed* — it fixed a real problem (an IO fault now lands on the game thread and would propagate into engine code). The design question "swallow or propagate?" was asked; "swallowed and therefore invisible" was not. No rule covers "a diagnostic that fails silently defeats its own purpose." | `_droppedToWriteFault` counter + a self-reporting `WARNING` line emitted on the next successful drain (written directly, cannot re-enter). Test: `LogInfo_AfterWriteFaultRecovers_ReportsTheLostLineCount`. |

Both fixed in-session; both RED-proven against `c53c8436` before the fix (finding 1 failed with *"Drain() left 200 item(s) queued after Dispose"*).

## Agent disagreement (resolved by reading the code)

Agent 3 (Efficiency) claimed *"blocking duration if collision: up to 50ms"* on the game thread. Agent 6 (Concurrency) refuted it. **Agent 6 is right:** `Thread.Sleep(50)` is in `ProcessQueue` (line 79), entirely outside `Drain()`'s `lock` (line 56) — the writer never holds the lock while sleeping. Agent 3 conflated *how often the writer wakes* with *how long the lock is held*. Real bound: one `Drain()` batch = queue depth × per-line write to page cache.

This is why the disagreement was worth surfacing rather than averaging: one agent's confident number was simply wrong, and the CHANGELOG's `~15ms` cost claim survives only because Agent 3's 50ms figure was refuted.

## Root-cause pattern: the fix's own risk surface had no reviewer

Both defects are in the same 20 lines — the concurrency rewrite — and both were invisible to the standing review apparatus. The changeset's *stated* risk (4 new Harmony bindings against a private engine method) was covered from three directions (Agent 1 standards, Agent 2 API, Agent 5 data flow) and was clean. The *actual* risk (a lock/liveness rewrite of the logger every other feature depends on) had **no rule, no agent, and no prompt asking about it** — it was reviewed only because the orchestrator noticed the gap and hand-rolled a 6th agent.

The 5 core agents are calibrated for TAOM's usual changeset: Harmony patches, GameModels, adapters, XML data. This changeset was mostly that, and the agents correctly passed it. The 40 lines that were *not* that carried 100% of the defects.

## Why each core agent missed these

- **Agent 1 (Standards):** rule set is ADRs + Harmony conventions. Nothing about threads. Correctly reported PASS — the code *is* standards-compliant.
- **Agent 2 (API compat):** scoped to TaleWorlds signatures. `FileLogger` touches no engine API. Out of scope by construction.
- **Agent 3 (Efficiency):** closest to a hit — it reasoned about lock contention, but its rule set frames locks as a *throughput* concern, not a *liveness* one. It asked "how long does this block?" (and got the answer wrong) instead of "can this loop fail to terminate?"
- **Agent 4 (Completeness):** counts tests/docs/issues. Both defects had passing tests around them; neither was a missing-artifact problem. Also **over-reported test counts** (claimed 15 FileLogger tests; the measured run showed 12) — a reminder that agent-reported counts are claims, not evidence.
- **Agent 5 (Data Flow):** traced 7 flows, 0 gaps — correctly. Its checks are about *data reaching consumers*, and the data flow genuinely was intact. A hot-spinning writer thread is not a data-flow gap.

## Lessons to codify

Appended to `docs/reviews/lessons/build-tooling-workflow.md`:

### When a rewrite adds an early return to a loop body, enumerate what the old unconditional path guaranteed

**Why missed:** The `FileLogger` rewrite added `if (_logFile == null) return;` at the top of `Drain()` for null-safety. The old code had no such guard — it dequeued unconditionally and wrote via `_logFile?.`, which *incidentally* guaranteed the queue always drains. That guarantee was load-bearing for `ProcessQueue`'s `!_queue.IsEmpty` exit condition, was never named anywhere, and had no test. The new guard was locally correct and globally a liveness regression.

**Prevent:** When adding a guard clause to a method a loop depends on, ask what the loop's exit condition reads and whether the guard can permanently prevent that condition from clearing. Pin the invariant with a test that asserts the *side effect* the loop needs (here: "the queue drains"), not just the absence of an exception.

**Source:** `docs/reviews/rca-battle-load-blind-window-2026-07-16.md` finding 1; commit `c53c8436` → fix.

### A diagnostic that fails silently is worse than one that fails loudly

**Why missed:** `Drain()`'s empty catch was deliberate — an IO fault on the game thread must not propagate into engine code, and the catch cannot log without re-entering itself. Both constraints are real. But "swallow" was treated as the end of the design, when the fault also needed a *channel*: a crash-forensics instrument that looks healthy while dropping lines is actively misleading during exactly the incident it exists to document.

**Prevent:** For any swallowed fault in a diagnostic/observability path, provide a signal that does not re-enter the faulting component — a counter surfaced on recovery, a one-shot sentinel, or a field the crash bundle reads. "It can't log from here" is a reason to find another channel, not to stay silent.

**Source:** `docs/reviews/rca-battle-load-blind-window-2026-07-16.md` finding 2.

### Review agents must cover the changeset's ACTUAL risk, not its nominal category

**Why missed:** The 5 core agents are calibrated for Harmony/GameModel/adapter/XML work and passed this changeset correctly — the Harmony half *was* clean. The concurrency half was reviewed only because a 6th agent was hand-rolled for it. Both confirmed defects were there.

**Prevent:** Before dispatching `/deep-review`, name the changeset's single riskiest property and check whether any core agent's rule set actually covers it. If not, write the extra agent — the core five are a floor, not a ceiling (the skill says so; this is the worked example). Candidate triggers: threading/locking, process lifetime, native interop, anything under `Main/Core/` that every feature depends on.

**Source:** `docs/reviews/rca-battle-load-blind-window-2026-07-16.md` root-cause pattern.

## Accepted, not fixed

- **Cross-thread timestamp vs file order (LOW).** The timestamp is stamped at `Enqueue` and the actual enqueue is a separate step, so under preemption two threads' lines can appear out of timestamp order. Pre-existing (unchanged by this commit) and effectively invisible at the format's 1-second resolution. Not worth a monotonic sequence number.
- **One wasted `Flush()` per drain race (LOW).** If the writer sees a non-empty queue and the game thread drains it first, the writer's `Drain()` flushes nothing. Self-correcting on the next loop iteration (it then takes the sleep branch). Not a spin.
- **Per-agent INFO during spawn is now synchronous (MED, accepted).** `LogAgentEquipBegin`/`Ok` fire per spawning agent, so a large battle turns ~1000 stamps into game-thread flushes. This is load-time, behind a loading screen, and bounded by page-cache writes (~15ms across a multi-second load). It is also the exact window the instrument exists to survive — making those lines async again would reopen the bug. Documented in the feature doc's durability caveat.
- **`MissionDiagnosticBehavior` "added LAST" comment is stale** (three `AddTaomBehavior` calls follow it). Pre-existing, unchanged by this commit; Agent 5 flagged it for awareness. Not in scope — worth a follow-up.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/battle-load-diagnostics.md](../features/battle-load-diagnostics.md)

<!-- backlinks-end -->

# RCA: off-thread agent removals in MissionDiagnosticBehavior (plan 056, deep review, 2026-11-18)

## Top line

Plan 056 (#607) made `MissionDiagnosticBehavior` park an `OnAgentRemoved` or `OnAgentDeleted` drop that
native raises off the main mission thread, and apply it at the next tick before any read. The tier 1
review (Lens 2 Engine compatibility, Lens 5 Data flow) found no thread-safety defect and confirmed the
plan's census of the 16 `OnAgentRemoved` overrides. It found two LOW defects: after a failed read the
behavior kept parking drops that nothing would ever drain, and the feature doc's "first one per
callback" warning line read as once per mission when it is once per game session. Both are fixed in
the review's commit. Codex did not run.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | LOW | After `CaptureActionSet` throws, `_readingStopped` (MissionDiagnosticBehavior.cs:88) makes `ReadPendingAgents` return before `Drain` (:80) for the rest of the mission, but `OnAgentRemoved` and `OnAgentDeleted` still called `RemoveOrDefer`, so every off-thread drop parked a closure and an `Agent` reference until `OnEndMissionInternal` cleared them. Bounded by one mission's off-thread removals; nothing read them. | Stale state / lifecycle | The plan saw the pile-up and accepted it as harmless (plans/056-agent-removed-off-thread-guards.md:952-953). The executor added a producer (the parking path) behind a drain that an existing latch can switch off, and checked the latch only against the old producer, `OnAgentCreated` (:66). | The drops now go through `DropPending` (:76), which skips when reads have stopped; test `AfterAReadFault_AnOffThreadDrop_IsNotParked` (red at `Actual:<2>`, then green). Lesson added to `lessons/state-lifecycle-save.md`. |
| F2 | LOW | docs/features/mission-diagnostic.md:26 said "the first one per callback writes one WARNING line"; `MissionThreadGuard._reportedSites` is static and only `ResetForTests` clears it (MissionThreadGuard.cs:22, :62-67), so the line is written once per game session. | Other: doc against code | The sentence was written from `DeferredCallbackQueue`'s "reports a site's first off-thread call" without tracing where the dedup set lives. | Doc now says "in a game session". One-off; no lesson. |

## Why each agent missed these

- **Agent 2, Engine compatibility:** found F1 and rated it LOW with the fix the review applied. F2 is a
  data-flow fact, outside its engine scope.
- **Agent 5, Data flow:** found both. It rated F1 (its trace 6) "no fix needed" because the plan had
  recorded it; the lead classed it as a defect because the existing latch closes it with one check and
  a recorded trade-off is still a trade-off the code need not make.
- **Agents 1, 3, 4 and 6:** skipped by tier.
- **The executor:** followed the plan, which had pre-judged the pile-up as harmless.

## Root-cause pattern

Both findings come from one theme: state whose lifetime is wider than its reader assumed. F1 is a queue
whose drain sits behind a latch that its producers ignore; F2 is a static dedup set described as if it
were per mission. The F1 shape (a latch stops the consumer but not every producer) has a cousin in
`lessons/state-lifecycle-save.md` "Diagnostics latches: state transitions unconditional, closers
enumerated per opener path", which enumerates closers per opener; F1 is the mirror case, producers per
stopped consumer.

## Left for later

- FOLLOW-UP, no issue: an /improve lead files none, and the orchestrator decides at wrap-up. The other
  `OnAgentDeleted` overrides were not checked against the off-thread question (plan :936-942).
- FOLLOW-UP, no issue: same reason. `OnAgentCreated` still adds inline on the assumption that the engine
  raises it on the main thread (plan :947-951).

## Feedback memories to codify

None. The lesson entry carries the rule.

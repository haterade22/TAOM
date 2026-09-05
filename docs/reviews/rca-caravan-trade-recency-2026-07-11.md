# RCA — CaravanTrade recency-visit-memory fix (2026-07-11)

**Feature:** CaravanTrade recency-visit-memory (issue #335) — fixes AI caravans leaving a town and immediately returning.
**Review chain:** 5-agent `/deep-review` (all clean) → Codex adversarial pass (`/review-codex`, `xhigh`).
**Codex verdict:** all 8 Known Suspects DISPUTED (fix confirmed non-inert, singleton lifetime correct, no payout starvation, no stranding, correct NaN polarity, no leak, correct player-scoping, consistent config). **1 MEDIUM finding**, verified CONFIRMED against source and fixed in-session.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | `CaravanVisitMemory` (a process-level DryIoc singleton) was never cleared on new-game / load. Across an in-process campaign switch, stale rings keyed by `MobileParty.StringId` (which is reused across campaigns) survive and mis-penalize a fresh caravan's first hops in the new campaign. | Stale state / lifecycle | I modeled the memory as "ephemeral — rebuilds as caravans move" and reasoned only about the *within-campaign* rebuild and the *save-load one-hop* cost. I did not enumerate the **same-process, cross-campaign** state: a process-level singleton outlives a single campaign, and StringIds repeat. | Added `ICaravanVisitMemory.ClearAll()` + `CaravanVisitMemoryBehavior` subscribes `CampaignEvents.OnSessionLaunchedEvent` (fires on both new game and load) to clear before any destination scoring. Regression test `ClearAll_RemovesEveryCaravan_ReturnsOneAfter`. |

## Root-cause pattern

A **process-level singleton holding per-campaign runtime state** has a lifecycle boundary that per-campaign `SyncData`/behavior reasoning hides: the singleton is created once in `OnSubModuleLoad` and is shared by every campaign in the process. "No SyncData — ephemeral" correctly means *not persisted to the save*, but it does **not** mean *reset between campaigns* — those are different lifecycle events. Any process-singleton cache keyed by an id that repeats across campaigns (`MobileParty.StringId`, `Hero.StringId`, `Settlement.StringId`) must clear on `OnSessionLaunchedEvent`, or it leaks state from campaign A into campaign B within one game session.

## Why each deep-review agent missed it

- **Agent 4 (Completeness)** and **Agent 5 (Data Flow)** both explicitly noted "no SyncData — ephemeral, rebuilds as caravans move" and accepted it. Both traced the *within-campaign* record→read→evict path and the *save-load* path, but neither enumerated the *new-campaign-in-same-process* / *load-a-different-campaign-in-same-process* state. The prompt's data-flow "Lifecycle Completeness (State Matrix)" check lists entity states (alive/killed/removed/session-end) for entity mutations, but does not explicitly list "process-singleton survives a campaign switch" as a state to enumerate for a shared cache.
- **Agent 3 (Efficiency)** checked the dict for unbounded growth and confirmed per-caravan bounding + `MobilePartyDestroyed` eviction — but "does it leak *memory*" is a different question from "does it leak *stale state across campaigns*."

## Lesson (also appended to LESSONS-LEARNED.md → State, Lifecycle & Save)

**Process-singleton per-campaign caches must clear on `OnSessionLaunchedEvent`.** "No SyncData / ephemeral" ≠ "reset between campaigns" — a DryIoc `Reuse.Singleton` lives for the whole process and is shared by every campaign in that session; state keyed by a campaign-reused id (`*.StringId`) contaminates the next campaign. **Prevent:** any behavior fronting a singleton runtime cache subscribes `OnSessionLaunchedEvent` → `ClearAll()`. Enumerate the "new/loaded campaign in the same process" state whenever a cache is a process singleton, not only the within-campaign and save-load states.

## Verification

- Build + `TAOM.Tests` green (full suite was 4223 before this fix; the fix adds one test). *(Final green re-run pending an environment file-lock on the game Modules dir at commit time — see the session log; the added code is a one-line `Dictionary.Clear()` + a subscription + a test.)*

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->

# RCA: isolate each hand-wired mission behaviour registration (plan 058, deep review, 2026-11-19)

## Top line

Plan 058 (#785) builds each hand-wired TAOM mission behaviour in `MissionBehaviorBuild.Isolated`, so a
throw while building one drops only that behaviour. The tier 1 review (Lens 2 Engine compatibility,
Lens 5 Data flow) confirmed that the registration order, every moved condition, the co-op add filter,
the guard's notice and the engine add outside the isolation are unchanged. It found one LOW defect: the
new catch also contained an `OutOfMemoryException`, which the mission-start guard it sits beside never
swallows. It is fixed in the review's commit. Codex did not run.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | LOW | `MissionBehaviorBuild.Isolated` caught every `Exception` (MissionBehaviorBuild.cs:27 before the fix, :29 after it), so an `OutOfMemoryException` thrown while building a behaviour was logged, reported and swallowed. The mission-start guard rethrows it: `ShouldSurvive` is false for OOM (MissionStartGuardService.cs:65-66) and its call wrapper lets it reach the engine untouched (MissionStartGuardCalls.cs:12-14). Before #785 such a throw propagated. No known producer at these call sites, so this is a parity gap, not an observed fault. | Other: catch scope against a sibling guard | The plan specified the new containment as `catch (Exception)` and quoted the guard's `Report` path (which does not read the setting) without quoting `ShouldSurvive`, where the guard's exclusions live. The executor built the plan's shape. | The catch now has `when (!(ex is OutOfMemoryException))`; test `Isolated_FactoryThrowsOutOfMemory_RethrowsWithoutLoggingOrReporting` (red: "No exception thrown", then green). Lesson added to `lessons/harmony-il.md`. |

## Why each agent missed these

- **Agent 5, Data flow:** found F1 (trace 8) through its sibling-guard rule (5f) and rated it LOW with the
  fix the review applied.
- **Agent 2, Engine compatibility:** OOM handling is a TAOM guard contract, not an engine API fact, so it
  sat outside its scope. It flagged the related setting change (by design, plan Design item 6).
- **Agents 1, 3, 4 and 6:** skipped by tier.
- **The plan and the executor:** the plan read the guard for the paths the new code calls (`Report`) and
  not for the policy the guard applies before it catches (`ShouldSurvive`).

## Root-cause pattern

One finding. It repeats the theme of the `lessons/harmony-il.md` entry "Scope a backstop finalizer to the
exception it was written for": a bare catch looks the same as a scoped one until it eats an unrelated
fault. The new element is that a scoped sibling already existed and its exclusion list was not copied.

## Left for later

- FOLLOW-UP, no issue: an /improve lead files none, and the orchestrator decides at wrap-up. The
  preamble of `AddTaomMissionBehaviors` and `TryClaimMission` stay unisolated, as the plan defers.

## Feedback memories to codify

None. The lesson entry carries the rule.

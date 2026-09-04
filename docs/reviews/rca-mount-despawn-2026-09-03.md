# RCA: MountDespawn deep review, 2026-09-03

Five agents reviewed the new dead-mount despawn feature. **Zero data-flow gaps, zero API
incompatibilities, zero completeness gaps.** Four findings were confirmed and fixed pre-commit;
three were disputed and rejected with reasons. No finding was deferred.

The feature itself is a new `Main/Features/MountDespawn/`: a thin `MissionBehavior` owning the
engine `Agent` handles, plus a pure scheduling service that sees only `int` and `float`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `ResolveDelaySeconds` fell back to 5s silently when the MCM delay was non-finite or outside [3,30]. No warning anywhere. | Config validation | The author read `.claude/rules/csharp-architecture.md` "Config Providers MUST Validate" for its NaN half and implemented that correctly, then stopped. The rule has six numbered requirements; requirement 5 ("log a warning and fall back") and 6 ("emit a summary warning when any reversion occurred") were never reached. | Fixed: `IModLogger` injected, warn-once-per-mission latch reset in `OnMissionEnd`. Two tests pin it. See "Root-cause pattern" below. |
| 2 | MED | A code comment claimed the fade loop was structurally safe against `FadeOut` re-entering `OnAgentDeleted`, but `CollectDue` returns the service's own reused `List<int>`, not a copy. Safe today only because `Forget` happens not to touch that buffer. | Fragile invariant | Author-written comment asserted a guarantee the author had reasoned to but not built. The reasoning was correct; the word "precisely so" made an accident sound designed. No agent but Data Flow would have opened both files at once. | Fixed: behavior copies into its own `_dueScratch` before iterating. The invariant is now local to one method instead of spread across two files. |
| 3 | LOW | `OnAgentRemoved` checked `_service.IsEnabled` (MCM static read) before `affectedAgent.IsMount` (inlined pointer read plus bitwise AND). Wrong order on a per-death callback. | Guard ordering | Guards were written in the order the author thought about them (feature-off first, then relevance), not in cost order. | Fixed: reordered. |
| 4 | LOW | `MountDespawnMissionGate.IsEligible` evaluated `mission.CombatType`, a native call through `MBAPI.IMBMission.GetCombatType`, before two field reads and three enum compares. | Guard ordering | Structure was copied from `DreadMissionGate`, which has the same ordering. Copying a sibling carries its cost profile along with its shape, and nobody had costed the sibling either. | Fixed: native call moved last. **`DreadMissionGate` has the same ordering and was not touched** (out of scope for this changeset); worth a follow-up look, since it runs on a per-tick gate. |

## Rejected findings

Recorded because a rejected finding that is not written down gets re-raised next review.

| Finding | Severity claimed | Why rejected |
|---|---|---|
| Cache `IsEligible` per mission to avoid a native call per dead mount | HIGH ×2 | The severity was asserted, not computed. `IsEligible` is reached only for a *killed mount*, not every agent death, so the worst case is a cavalry clash at roughly 30 calls per second: about 3000 cycles by the agent's own per-call estimate. A cache buys that back and costs invalidation logic plus exactly the stale-gate hazard the code comments exist to prevent, since `MissionTeamAIType` is assigned after `OnBehaviorInitialize`. Tiny win, real complexity: rejected per `simplicity-criterion.md`. The skill's own rule says an unverified cost claim is reported UNVERIFIED, never HIGH. |
| Drop the `IsEnabled` check from `OnMountKilled` so the service trusts its caller | MED | Deletes the service's own contract and a passing test to save one static read per dead mount. A service that is only correct when called correctly is worse than one static read. |
| Cache `IsEnabled` at tick level; the re-read comment conflates two reasons | MED | The comment is accurate as written: `MissionTeamAIType` genuinely is assigned after `OnBehaviorInitialize` (so the gate cannot be cached at init) and the toggle genuinely can flip mid-battle. Both reasons are real and both are stated. |

## Root-cause pattern: partially-applied multi-requirement rules

Findings 1 and 2 share a shape. In both, the author knew the relevant rule, applied the part that
was top-of-mind, and stopped at a point that felt complete.

- Finding 1: "Config Providers MUST Validate" is six numbered requirements. The NaN requirement is
  the famous one (five prior shipped instances, called out in the rule text), so it got applied and
  the surrounding five did not. **The rule's own notoriety on one point crowded out its other
  points.**
- Finding 2: the re-entrancy hazard was correctly identified and correctly reasoned about. What was
  skipped was converting the reasoning into structure. A comment recording an invariant is not the
  same artifact as code enforcing it.

**Prevent:** when a rule is a numbered list, walk the numbers. When you write a comment asserting a
safety property, ask whether anything but the comment enforces it; if not, either enforce it or
write the comment as "safe today because X", never as "structurally safe".

## Why each agent missed what it missed

| Agent | Caught | Missed, and why |
|---|---|---|
| 1 Standards | Finding 1 | Findings 3 and 4 are cost ordering, outside its rule set. Finding 2 needs two files open at once. |
| 2 API compatibility | Nothing to catch: 19 members verified, 0 incompatible | Correctly scoped to signatures. It did the highest-value thing available to it, which was proving `FadeOut`-on-a-dead-agent is *not* knowable from managed code rather than guessing. |
| 3 Efficiency | Findings 3 and 4 | Over-rated three findings as HIGH without computing a cost, against the skill's explicit instruction. Its correct findings were real; its severities were not. |
| 4 Completeness | Nothing to catch: COMPLETE | Would not have caught 1 (the test existed and passed; it pinned the fallback value, not the warning). |
| 5 Data flow | Finding 2 | The only agent that opened service and behavior together and read the returned buffer's identity. This is the fourth consecutive review where the data-flow agent found the finding no per-file agent could have. |

## Lessons to append

One durable entry, to `docs/reviews/lessons/gamemodels-services.md` (config-validation half) and one
to `docs/reviews/lessons/state-lifecycle-save.md` (invariant half). Both are recorded below in house
shape.

### A comment asserting an invariant is not enforcement

**Why missed:** the reasoning behind the comment was correct, so re-reading the comment confirmed
the reasoning rather than testing whether anything held it in place. The phrasing ("precisely so")
made an accidental safety property read as a designed one.
**Prevent:** when writing a comment that asserts a safety property, name what enforces it. If the
answer is "nothing, but no current caller violates it", write that instead, or spend the few lines
to make it structural. A cross-file invariant with no enforcement is one edit from a bug and the
comment will still read as true afterwards.
**Source:** `docs/reviews/rca-mount-despawn-2026-09-03.md` finding 2.

### Walk every numbered requirement of a multi-part rule

**Why missed:** "Config Providers MUST Validate" leads with the NaN requirement, which has shipped
five times and carries the most prose. That prominence consumed the attention the other five
requirements needed, and the validation felt done once NaN was handled.
**Prevent:** when a rule is a numbered list, enumerate the numbers against the code rather than
against memory of the rule. Requirement 5 of that rule (log a warning on every reversion) is the one
that gets dropped, because a correct fallback value looks like a finished job.
**Source:** `docs/reviews/rca-mount-despawn-2026-09-03.md` finding 1.

## Follow-up not in this changeset

`DreadMissionGate.IsEligible` orders its native `CombatType` read first, the same way this feature's
gate did before finding 4. It runs on a per-tick gate. Not touched here because it is outside this
changeset's scope, and edit-scope discipline says a drive-by fix belongs in its own change.

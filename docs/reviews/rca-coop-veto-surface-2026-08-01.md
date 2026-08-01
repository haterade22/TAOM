# RCA — the co-op veto surface was gated at one call site out of four (2026-08-01)

**Feature:** CoopInterop / #370 · **Trigger:** `/deep-review`, 6 agents · **Verdict at review:** NEEDS FIXES (3 HIGH)

## Top-line

The 2026-08-01 fix gated TAOM's diplomacy vetoes so a co-op host's decisions could not be silently
overturned by a peer. It gated **the Harmony prefixes**. The identical rules are also reachable
through **two GameModel overrides**, which were left enforcing — including the one rule this project
had already *confirmed* diverges between peers (`ShouldBlockPeace` → `WarOfTheRing_CurrentPhase`, a
TAOM `SyncData` key no co-op mod replicates).

Separately, the same changeset suppressed the time-acceleration **button** while leaving the
**keybinds** that drive the same service ungated.

Both are the same mistake: **fixing the path that was being looked at, rather than enumerating every
path to the behaviour.** The original D1 finding was itself described as "`Priority.High` was half a
fix" — and the fix for it was also half a fix. That repetition is the real finding here.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | `TaomKingdomDecisionPermissionModel`'s 3 overrides re-apply the war/peace/alliance rules with no co-op gate. Engine reaches them via `DeclareWarDecision.IsAllowed()` / `MakePeaceKingdomDecision.IsAllowed()` — a different call site from `ApplyInternal`. Effect: a peer with drifted WotR phase silently cannot *propose* a peace the other peer proposes normally. | Cross-system data flow | Fixed the call site named in the finding (the prefixes) and never asked "what else calls this rule?" | `CoopVetoClassificationTests.EveryConsumerOfADivergenceProneDiplomacyRule_AlsoReadsTheCoopFlag` — scans all of `Main/` for consumers of the three rules and fails the build on any that reads no co-op flag |
| 2 | HIGH | `TaomDiplomacyModel.IsAtConstantWar` — a **third** ungated `ShouldBlockPeace` consumer. Makes peace permanently unreachable between two factions, so peers disagree about which wars can ever end. | Cross-system data flow | **Missed by the review agent that found #1**, and by me. Found only by grepping every caller of the rule while fixing #1 | Same test as #1 — it is caller-enumerating by construction, so it cannot miss a sibling the way a human or an agent reading one file can |
| 3 | HIGH | `TimeAccelerationService.OnTick` ungated: E / Space / Ctrl+Space still ran under co-op after the button was suppressed. Writes `Campaign.SpeedUpMultiplier`, a *different* property from `TimeControlMode` — the co-op host's setter prefix covers the latter, nothing is known to cover the former | Toggle/UI-vs-mechanic split | Treated "remove the widget" as equivalent to "disable the feature". The widget was the only surface I enumerated; the keybind path never touched it | Rule: when suppressing a feature under co-op, gate the **service**, not the presentation. Widget removal is cosmetic unless the service is the only way in |
| 4 | HIGH | UI registration decided from `CoopPresence` probe #1 — the one the code's own comments flag as possibly-too-early — while every other consumer reads live after the authoritative probe #2. Silent failure mode: solo branch taken, no log line at all | Lifecycle / init ordering | I flagged this as the highest risk *before* dispatching agents and shipped it anyway rather than fixing it first | `Refresh()` immediately before the read, plus an unconditional log line on **both** branches so the boot matrix can distinguish "genuinely solo" from "detected too late" |
| 5 | LOW | `ICoopPresenceProvider` doc comment enumerated the co-op mods; BannerlordCoop was added to the real list and not the comment, within a day | Doc drift | Inline copy of a list that lives elsewhere | Comment now points at `CompiledModuleDefaults` instead of restating it |
| 6 | — | Efficiency agent reported a MEDIUM hot-path lock on `CoopPresence.IsActive`, claiming "100 decisions/tick" | Disputed | — | **Rejected.** `ApplyInternal` is private, reached only from 8 `ApplyByX` methods — discrete events, not per-tick. Proposed cache also risks a stale read against `Refresh()`. `simplicity-criterion.md`: tiny win + correctness hazard |

## Root-cause pattern: fixing the instance, not the surface

Findings 1–3 share one shape. In each case a *behaviour* (block a war / block a peace / accelerate
time) is reachable through more than one *mechanism*, and the fix was applied to the mechanism that
happened to be in the diff:

| Behaviour | Mechanism gated | Mechanisms missed |
|---|---|---|
| Block war | `DeclareWarAction.ApplyInternal` prefix | `IsWarDecisionAllowedBetweenKingdoms` |
| Block peace | `MakePeaceAction.ApplyInternal` prefix | `IsPeaceDecisionAllowedBetweenKingdoms`, `IsAtConstantWar` |
| Accelerate time | `MapBar` widget registration | E / Space / Ctrl+Space keybinds |

The generalisable rule: **gate the rule, not the call site.** Before gating any behaviour on co-op,
grep every caller of the underlying service method and every input path that reaches the service.
If the answer is "more than one", either gate at the choke point or add a test that enumerates
callers — do not hand-gate N sites and trust that N was the real count. It was not, twice, in one
changeset.

## Why each agent missed these

| Agent | Why |
|---|---|
| 1 Standards | Scope is ADR compliance. An ungated veto is architecturally *correct* code — right layering, right injection, right thinness. Nothing to flag |
| 2 Compatibility | Scope is signature verification against v1.4.7. Every signature was valid; the bug is semantic |
| 3 Efficiency | Scope is allocation and call frequency. Also produced the one false positive, by asserting a call frequency it never measured |
| 4 Completeness | Checks that tests/docs/issue/CHANGELOG *exist*. All did. It cannot ask whether the tests cover the right paths |
| 5 Data flow | **Found #1 and #4.** Missed #2 — it traced from the changed files outward and `TaomDiplomacyModel` was not in the changeset, so it never opened it |
| 6 Seam | Scope was the two parallel workstreams' shared file. Correctly reported that seam clean. Also caught a transient build break from a third in-flight workstream — real when observed, self-resolved by the time I verified |

The honest summary: **the changed-file boundary is what hid #2.** Five of six agents were given the
changeset as their scope, and the missing gate was in a file the changeset never touched. A review
scoped to the diff cannot find a bug whose evidence is a file outside the diff.

## Preventive actions taken

1. `CoopVetoClassificationTests.EveryConsumerOfADivergenceProneDiplomacyRule_AlsoReadsTheCoopFlag` —
   scans **all** of `Main/`, not the diff, for consumers of `IsWarAllowed` / `ShouldBlockPeace` /
   `IsAllianceDecisionAllowed`, and fails the build on any that reads no co-op flag. Verified
   non-vacuous: it detects exactly the 4 real consumers, two of which read UNGATED before this fix.
2. The existing prefix registry's own blind spot is now documented in the test file — it scanned
   only Harmony prefixes, so a GameModel override could never appear in it.
3. `Refresh()` before the UI-registration read, plus a log line on both branches.
4. Doc comments that restate a list elsewhere replaced with a pointer.

## Feedback memories to codify

- **Gate the rule, not the call site.** Before gating a behaviour on any environment flag (co-op,
  toggle, feature flag), enumerate every caller of the underlying service method and every input
  path that reaches it. Hand-gating N sites is only correct if you proved N. Prefer a
  caller-enumerating test over a hand-maintained list.
- **A review scoped to the diff cannot find a bug outside the diff.** When a changeset gates a
  shared rule, at least one agent must be scoped to *the rule*, not to the changed files.
- **Suppressing a widget is not disabling a feature.** Gate the service; the UI is one input path
  among several.

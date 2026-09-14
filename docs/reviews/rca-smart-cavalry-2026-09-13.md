# RCA: SmartCavalryAI shipped for four months without ever forming a line (#586)

**Date:** 2026-09-13
**Scope:** the v1 feature as shipped 2026-05-06 (#112), the v2 rework that replaces it, the 5-agent
deep review of v2 and the Codex adversarial pass on v2.
**Trigger:** player feedback relayed by the maintainer: "cav stop in the middle of the enemy or don't
react at all to nearby enemy troops", "you also have to double charge them (F3 twice) for them to
actually charge". Other players reported the same.

## Top-line

SmartCavalryAI v1 could not work. Its line-up issued a `Stop`, which the engine implements as every
rider holding its own position, so no line ever formed; its alignment check measured spread ALONG
the line and demanded 1.5 m of it, so it could never pass for more than two riders; and the second
F3 was swallowed by an idempotency guard, which is why the second press "worked" (vanilla took the
formation). The feature carried 44 green unit tests, a 5-agent deep review, a Codex adversarial pass
and a feature doc that said "Status: Pending in-game verification" for the whole period. Nothing in
that pipeline asked whether the gate the machine waited on could occur, because the gate lived in an
adapter and every test mocked it.

v2 replaces the machine (Move-based line-up, arrangement-slot alignment, frozen-direction contact,
reform past the live enemy, a hit-and-run loop, dwell budgets, hand-off to a vanilla charge, cancel
on other orders, an `IsAIControlled` gate). Its deep review found one defect in the rework itself
(the engine re-issues a formation's own order when a banner bearer dies, which read as a player
order and cancelled the cycle), one lifecycle hole (toggle off mid-cycle left riders on a Move), two
inverted NaN gates, a hot-path allocation my alignment read triggered in another feature's prefix,
and a settings hint that described a HUD that never existed. All fixed in-session.

## Part 1: why v1 shipped broken (the incident)

| # | Sev | Defect | Category | Why missed | Preventive action |
|---|-----|--------|----------|-----------|-------------------|
| 1 | HIGH | `InitiateLineCharge` and `UpdatePassingThrough` issued `Stop` to "form a line" and to "reform". `MovementOrderStop` is `StandGround` (`MovementOrder.cs:143-144`), under which `Formation.GetOrderPositionOfUnit` returns each rider's OWN position (`Formation.cs:1262`); the `SetPositioning` line was never consulted. Riders froze where they stood, in or near the enemy. | Missing vanilla gate (order semantics assumed from the name) | The port kept the donor's order choice. Nobody opened `GetOrderPositionOfUnit`'s switch on `MovementState`. The v1 deep review verified that `SetPositioning` and `SetMovementOrder` EXIST with the right signatures (they do) and never what the Stop branch does to the positioning just applied. | Lesson in `lessons/adapters-taleworlds-api.md`: before issuing an order to shape a formation, read `MovementOrder.MovementState` for that order and the matching `GetOrderPositionOfUnit` branch. v2 issues Move for every line-up and reform. |
| 2 | HIGH | `FormationAdapter.IsAligned` projected riders onto `Direction.RightVec()` and required max deviation under `5 * (1 - strictness)` (1.5 m at the default). `RightVec` is the perpendicular (`Vec2.cs:277`), the axis a line is spread along; the deviation is half the line's width. False for more than two or three riders at every strictness. Forming and Reforming never exited. | Logic error behind a mocked adapter | Every service test mocked `IsAligned(...)` to return the value the transition under test needed. The metric itself had no numeric test. ADR-008's "adapters are covered via service tests" is right for a thin wrapper and was applied to a wrapper that computed the feature's only load-bearing verdict. | Lesson in `lessons/testing-qa.md`. v2 extracts the decision into `LineAlignment` (pure, 12 numeric tests) and measures mean rider-to-slot distance from `Formation.Arrangement`. |
| 3 | MED | The idempotency guard in `HandleChargeOrder` returned when state was non-Idle, leaving the vanilla Charge the postfix had just observed in force; state stayed Forming for the rest of the battle. The second F3 "worked" because the feature had stepped aside. | Convention inconsistency (a guard written for a double-tap that never asked what the vanilla side effect was) | The guard was itself a deep-review fix ("double-tap idempotency") and had a passing test that asserted no extra commands were issued, which is exactly the wrong thing to want. | v2: a charge order mid-cycle means "charge now" and re-points the target (`ChargeNow`). |
| 4 | HIGH | `UpdatePassingThrough` measured 25 m from a position snapshot taken at order time and then issued `Stop` (see #1). `UpdateCharging` and `UpdatePassingThrough` never checked target liveness; an emptied `ChargeToTarget` target degrades vanilla's order into a hold at the order position (`MovementOrder.cs:536-538, 816-817`). | Stale state; missing vanilla gate | The snapshot was the donor's design; the target-death path was never enumerated. | v2 reads the live target every tick and re-targets the nearest enemy on target death. |
| 5 | MED | The postfix ignored every non-charge order, so a Move, Follow or Retreat issued mid-cycle was clobbered by the machine's next order. | Lifecycle (no exit for a displaced cycle) | The design enumerated how the machine starts and never how it yields. | v2 cancels on any non-charge player order. |
| 6 | MED | No `Formation.IsAIControlled` gate: with F6 delegation, an enlisted battle or a dead player, the team AI's Charge orders entered the machine and fought the AI. | Missing vanilla gate | `FormationAI.TickOccasionally` gates on `IsAIControlled` (`FormationAI.cs:284-290`); nobody asked who else calls `SetMovementOrder`. | v2 gates on `IsAIControlled` at the postfix and in the service tick. |
| 7 | LOW | The feature doc said "Status: Pending in-game verification" from the day it shipped and the pipeline treated that as a status, not a defect. | Process | Completion was judged by build-green and review-green. | Lesson in `lessons/testing-qa.md`: a shipped feature doc that still says pending in-game verification is an open defect with an owner. |

### Root-cause pattern: a green mock proves the reaction, never the stimulus

Defects 1, 2 and 3 share one shape. The tests pinned what the machine does WHEN aligned, WHEN the
target dies, WHEN a second order arrives; none asked whether "aligned" could occur, what a Stop does
to a line, or what vanilla did with the second order the guard ignored. All of those live at the
engine boundary, which is exactly the surface the adapter pattern hides from the tests. The cure is
not fewer mocks; it is one numeric test for every computed verdict behind an adapter, and one
decompiled fact for every engine order the feature issues (what does this order do to each unit).

### Why the v1 reviews missed it

- **Deep review (2026-05-06, 5 agents).** Standards, efficiency and completeness had nothing to see.
  Compatibility verified signatures, which were correct; nothing in its brief asked for the
  semantics of `Stop`. Data flow traced settings to consumers and found the strictness setting
  consumed in both states, which it was; the value it fed could never satisfy the comparison.
- **Codex adversarial pass (2026-05-06).** Its brief was written around the port's deltas (NaN in
  `Clamp`, the MixedFormations hand-off, the recursion guard, self-charge), all of which it improved.
  "Walk the machine with numbers" was in the prompt and was answered for the transitions, not for
  the geometry inside `IsAligned`.
- **Nobody ran the golden path.** The issue body's own verification section lists it; the doc's
  status line records that it never happened.

## Part 2: the v2 deep review (2026-09-13, 5 agents + 1 re-check)

| # | Sev | Finding | Category | Why missed by the author | Preventive action |
|---|-----|---------|----------|--------------------------|-------------------|
| D1 | HIGH (compat) | `BannerBearerLogic.FormationBannerController.RepositionFormation` re-issues the formation's current order (`BannerBearerLogic.cs:157`) whenever a bearer dies or is reassigned. Under a Move-holding state that is a non-charge order and the postfix cancelled the cycle with no player intent behind it. | Missing vanilla gate (caller enumeration) | The design enumerated the player's orders and the AI's, the two callers a reviewer thinks of. The compatibility agent found it because its brief asked "who ELSE calls the target" and it decompiled the whole assembly (63 call sites). | Prefix captures the previous order as `__state`; the postfix skips the cancel when `MovementOrder.AreOrdersPracticallySame(previous, input, isAIControlled: true)`. Lesson in `lessons/harmony-il.md`. Binding test pins both members. |
| D2 | MED (data flow G2) | Toggling the feature off mid-cycle left the formation on a Move the machine had issued; both entry points early-returned on `IsEnabled`, so nothing lifted it. | Lifecycle (toggle gates state transitions, cf. `harmony-patches.md` "Latches") | The `IsEnabled` gate was inherited from v1 and sat above the tick that enforces the "never stand still" invariant. | `HasActiveCycles` keeps the behavior ticking while a cycle exists; the service tick hands it back to a vanilla Charge when disabled. Test `Tick_FeatureDisabledMidCycle_HandsBackToVanillaCharge`. |
| D3 | HIGH (efficiency) | `IsAligned` called `Formation.GetOrderPositionOfUnit` per rider per tick while lining up; MixedFormations' Patch30 prefixes that method and allocates a `FormationAdapter` on every call. | Cross-feature hot path | I chose the public method because the engine drives riders to it; I did not check who prefixes it. The engine already calls it tens of thousands of times a frame, so the true cost was small, but the fix also removed two correctness holes (D4). | Read the slot from `Formation.Arrangement.GetWorldPositionOfUnitOrDefault(unit)` directly. |
| D4 | MED (data flow G4 + item 5) | Through the public method, a detached unit's "slot" is its detachment frame, and a slot the navmesh rejects falls back to the rider's own position, which reads as distance zero and inflates alignment. | Missing vanilla gate | Same read as D3; I read the Hold branch and not the two branches above it. | Same fix as D3: the arrangement has neither behaviour. |
| D5 | LOW (data flow I1) | `InitiateLineCharge` (`if (LengthSquared < 1f) return false`) and `CavalryPathPlanner` (`if (length < 1f) return false`) let a NaN through to `Normalized()` and the division; downstream `WorldPosition.IsValid` masked it. | NaN polarity (7th recorded instance of the class) | I wrote the contact gate as a positive requirement and copied the v1 shape for the two distance gates in the same file. | Both flipped to `!(x >= 1f)`; tests with a NaN target on both. |
| D6 | LOW (data flow G5) | `SmartCavalryDebug`'s hint promised HUD messages; no HUD path exists in the feature and the service's transition log is unconditional. | User-facing promise mismatch (pre-existing hint) | The hint was inherited; the review's toggle-coverage rule caught the read site count and the wording together. | Hint rewritten to describe the file log. |
| D7 | MED (data flow G3) | `ApplyCollisionAvoidance` nudged AI-controlled cavalry too. | Gate applied to one path (cf. `rca-siege-guards-2026-07-16.md`) | I added the `IsAIControlled` gate to the service and the postfix and not to the per-agent nudge in the same tick. Same shape as the 2026-07-16 HIGH on this very file. | Gated `_settings.IsEnabled && AvoidFriendlies && !cav.IsAIControlled`. |
| D8 | info (compat + data flow I2) | `Formation.Tick`'s substitute loop issues a plain Charge when a `ChargeToTarget` target empties, outside the recursion guard; it re-enters the postfix as a charge order and lands in `ChargeNow`, the same re-target `UpdateCharging` performs. Idempotent; possibly on the async AI thread, under the service lock. | Documented, not changed | Two independent paths to one re-target. | Documented in the registry and the patch's doc comment; test `HandleChargeOrder_WhileCharging_RetargetsWithoutRelining`. |
| D9 | info (data flow G1) | `Formation.RemoveUnit` flips an emptied player formation to `IsAIControlled` and nothing resets it on refill (`Formation.cs:2201-2203`), so the machine stays out of a refilled formation for the rest of the battle. | Vanilla behaviour, accepted | The gate mirrors the engine's own switch; the team AI commands such a formation in vanilla too. | Known limitation in the feature doc. |
| D10 | info (data flow R3) | With the direction frozen at line time, an enemy deeper than `ReformDistance` along that axis can contain the reform point. | Design trade-off | Chosen so flank charges register at the enemy's plane and keep their lateral offset. | Documented; `Tick_ChargingTargetMovedLaterally_ReformPointClearsTheLiveEnemyPlane` pins the behaviour; the MCM distance is the knob. |
| R1 | refuted (standards) | Five long dashes flagged in `IFormationAdapter.cs` and `SmartCavalryRecursionGuard.cs`. | | All five are pre-existing lines (the only dash in my hunks is a removed line); `output-style.md` exemption 4. | None. |

### Why each agent missed D1 to D7 (and who caught them)

| Agent | Caught | Missed and why |
|-------|--------|----------------|
| 1 Standards (haiku) | R1 only | Scoped to ADR compliance; correct within its remit. |
| 2 Compatibility (sonnet) | D1, D8 | Found D1 only because the brief asked for every caller of the target; a signature-only brief would have passed it. |
| 3 Efficiency (haiku) | D3 | Correctly refused to call the per-order lookups HIGH; it decompiled the engine calls it costed. |
| 4 Completeness (haiku) | none | Its remit is tests/docs/issue/IoC; it listed the stale doc statements the doc pass then used. It wrongly called the Patch31 registry section current. |
| 5 Data flow (sonnet) | D2, D4, D5, D6, D7, D9, D10 | Its brief was to FALSIFY the design's claims and to read the whole feature folder, not the diff; every finding it made was outside the lines I changed or in engine branches adjacent to the ones I read. |
| Re-check (sonnet) | see Part 3 | Ran on the fix deltas only. |

### Root-cause pattern for the v2 findings: I read the branch I needed and not its neighbours

D1, D4, D5 and D7 are all one step away from a line I read carefully: the caller list around the
method I patched, the two `switch` branches above the Hold branch I cited, the sibling distance gate
two methods above the one I wrote as a positive requirement, the per-agent nudge in the same tick
loop I gated. `harmony-il.md` already says "grep every call site" and `csharp-architecture.md`
already says "gate every float decision path in a touched method, not only the lines you added";
the author applied each to the line at hand and stopped. The review structure that catches this is
the one that worked here: one agent told to falsify rather than verify, reading the whole feature.

## Part 3: re-check of the fix deltas and the Codex adversarial pass

### 3a. Re-check of the fix deltas (one sonnet agent, fix files only)

Confirmed against the installed DLL: Harmony passes a struct `__state` from the prefix to the postfix
by value and scopes it to Patch31's own pair, so Patch35 (postfix only, same category) is untouched;
the postfix's `input` is the reassigned value (`Invalid` became `Stop` inside the target), which is
the value the formation actually received; `AreOrdersPracticallySame` short-circuits on the enum
before any field read, so a `default` previous order cannot throw; the NaN flips are identical to
the old gates for every finite input including the boundary; every new binding pin resolves. Two
defects in the fixes themselves, both fixed in-session:

| # | Sev | Finding | Why missed | Fix |
|---|-----|---------|-----------|-----|
| C1 | MED | `OnMissionTick` skips a formation with `CountOfUnits == 0` BEFORE calling the service, so a formation that empties mid-cycle keeps its non-Idle state until mission end and `HasActiveCycles` (D2's fix) stays true, defeating the disabled-feature early return for the rest of the battle. | The D2 fix added a predicate over the state dictionary without asking which formations the loop would never tick again. The empty-formation skip predates v2 and was read as "nothing to do". | `if (formation.CountOfUnits == 0) { _service.CancelCharge(formation); continue; }`; `CancelCharge` is a no-op without a live entry and issues no order. |
| C2 | LOW | `ColumnFormation.GetWorldPositionOfUnitOrDefault` returns null for EVERY unit, so under a Column arrangement the D3 fix collects no distances and `LineAlignment` reads an empty list as aligned; the line-up wait is skipped. | I verified `LineFormation` and did not decompile the other arrangements. The empty-list contract was written for "nothing to line up", not "cannot measure". | `IsAligned` returns false when no unit produced a slot, so the line-up budget decides; `LineAlignment`'s own contract and tests are unchanged. |

One accepted residual it named: a player Move within 1 m of the machine's own line or reform point
reads as a re-issue and does not cancel; the machine's next order then displaces it. Bounded and
improbable, kept as a known limitation.

### 3b. Codex adversarial pass (gpt-6-astra, ultra, about 75 minutes)

Codex decompiled every cited engine member fresh from the installed DLL, ran a compiled Harmony
probe to settle the prefix/postfix state question, and could not build or test (MSBuild SDK
discovery is denied inside its sandbox; it said so rather than substituting a stale run). Verdict:
3 P1, 4 P2. Every finding was verified against the source or the installed DLL before acting.

| # | Codex | Mine | Finding | Why missed by the author and by the deep review | Action |
|---|-------|------|---------|--------------------------------------------------|--------|
| F1 | P1 | P1, confirmed | `IssueChargeToTarget` issued the order and stopped. Every `SetMovementOrder` ends with `SetTargetFormation(null)` (`Formation.cs:714`), whose setter pushes target index -1 to every rider (`:222-238`), so the riders were on a free charge at anyone while the machine measured contact against a formation they were not necessarily attacking. Vanilla's own targeted charge sets the target AFTER the order (`OrderController.cs:812-817`). | I read line 714 during planning and filed it as a quirk with no consumer, because nothing in the FEATURE read `Formation.TargetFormation`; the consumer is the native agent, through the property setter. The data-flow agent traced the same line and reached the same wrong conclusion ("not load-bearing"). | Adapter re-sets the target inside the guarded scope. Binding pin on `SetTargetFormation`. |
| F2 | P1 | P1, confirmed | The player's "charge THAT formation" is `SetOrderWithFormation(Charge, B)`: a plain Charge, then `SetTargetFormation(B)`. The postfix runs between the two, sees `Charge` with no target, and started the cycle at the nearest enemy A; the player's choice was lost. v1 had the same shape (it read `input.TargetFormation`, which the player path never sets). | I grepped `OrderController` for `OrderType.Charge` during planning and saw both call sites, including line 816's `SetTargetFormation(orderFormation)`, and did not connect it to target resolution. The five agents reviewed the postfix's inputs, not the controller that produces them. | New `Patch31b_FormationSetTargetFormation` postfix and `ICavalryChargeService.RetargetCycle`: a Forming line is redrawn at the chosen target, a Charging formation re-issues its charge at it. Binding pin on the parameter name. |
| F3 | P1 | P1, confirmed | `ApplyChargeLine` wrote a direction through `SetPositioning` and never touched `FacingOrder`; `Formation.Tick` re-applies `FacingOrder.GetDirection` through `SetPositioning` every tick (`Formation.cs:2311-2314`), so the line faced the deployment direction again one tick later, and the "reform facing the enemy" never held. | The design treated `SetPositioning`'s direction as the formation's facing. Nobody in the pipeline decompiled `Formation.Tick`'s tail; the compatibility agent verified `SetPositioning`'s signature and null-argument semantics, which are correct and irrelevant to persistence. | Adapter installs `FacingOrderLookAtDirection(direction)` with every line. Binding pins. |
| F4 | P2 | P2, confirmed | The behavior skipped non-cavalry formations before the service tick, so a formation whose riders dismounted mid-cycle kept its Move forever with no budget running and no disabled hand-off. | Same shape as C1 (the empty formation), one predicate over: I fixed the `CountOfUnits == 0` skip and left the `RepresentativeIsCavalry` skip in place. | The behavior ticks every live formation; the service hands a no-longer-cavalry formation back to a vanilla charge. |
| F5 | P2 | P2, confirmed | The re-issue test used the engine's 1 m Move tolerance, so a player Move within 1 m of the machine's own Move point escaped the cancel. | The re-check agent had named it as an accepted residual; Codex showed a deliberate redraw (`MoveToLineSegment` to the same centre) reaches it. | Move re-issues now require the identical position within 1 cm; the residual is documented. |
| F6 | P2 | already fixed (C2) | Column arrangement reports no slot for any unit; an empty distance list read as aligned. | Found by the re-check agent and fixed before the Codex report landed. | None further; Codex's suggestion to install a Line arrangement is noted, not adopted (the player's arrangement is theirs). |
| F7 | P2 | P2, confirmed | The reform point cleared the plane through the enemy's centre by `ReformDistance`, not the enemy's occupied depth; a column or a line turned sideways could contain it. The complaint this change exists to fix. | I had accepted this as a design trade-off (D10) with the MCM distance as the knob. Codex's example (`x=100` front, ranks to `x=160`, `R=25`) made it concrete. | `GetTargetDepthAlong` on the adapter; the reform point adds the target's depth along the charge axis. |
| obs | P3 | fixed | The postfix's `IsEnabled` gate sat before the cancel branch, so a newer player order given while the feature was off could be replaced by the pending hand-off's charge; the service checked the toggle before AI ownership. | The toggle was read as "the feature is off, do nothing" instead of "the feature must not START anything". | Cancel and AI-ownership run regardless of the toggle; only starting a cycle needs it. |
| obs | P3 | fixed | Repeat cycles skipped the friendly-reroute planner, so the `AvoidFriendlies` promise held for the first charge only. | The loop was written as "line at the nearest enemy" and the reroute decision lived only in the entry point. | `StartCycle` runs the planner for every cycle. |
| obs | P3 | fixed | The strictness hint claimed "0 = launch immediately, 1 = perfect line"; the metric is a mean slot distance of 10 m / 4.4 m / 2 m and the budget launches regardless. | Inherited hint text. | Hint rewritten. |
| obs | info | corrected | `FormationAI.SetCurrentOrder` does not exist; the `IsAIControlled` gate is in `TickOccasionally` (`FormationAI.cs:284-290`). The name appeared in a patch comment, the registry, the feature doc, a lesson and this RCA. | The compatibility agent's report named it and I copied the name into five places without grepping it: the "verified paragraph, unverified proper noun" lesson from 2026-09-06, again. | All five corrected. |
| disputed | | | Codex disputed three handed suspects with evidence: the Harmony pairing (probe run), the Stop/Follow re-issue variants (they cancel on the first order), and the navmesh-fallback mismatch for a placed Line rider (the fallback runs only when the arrangement has no slot). All three accepted. | | |

**Codex's own verification limit:** its sandbox denied MSBuild SDK discovery, so it built nothing;
the 8817-green result it was handed was labelled as supplied evidence, not reproduced. Its report
was assembled to the path this session was also redirecting the CLI transcript into; the transcript
won, and the report was rebuilt from Codex's own part files (`.tmp/codex-cav-review-20260913/`,
gitignored) with its `assemble_report.py`.

### Root-cause pattern for the Codex findings: I verified what the engine method IS and not what it leaves behind

F1, F3 and F2 are all about the state a call leaves for the NEXT frame or the NEXT call: the target
index the setter clears on its way out, the facing the tick re-applies, the target the controller
assigns after the order returns. Every signature was verified; none of the three post-conditions
was. The review structure that catches this is the one Codex applied: walk the engine's own tick
after the write, and walk the engine's own caller before the entry point.

## Lessons codified

- `docs/reviews/lessons/adapters-taleworlds-api.md`: a Stop order holds each rider where it stands;
  shape a formation with a Move; read the slot from the arrangement.
- `docs/reviews/lessons/testing-qa.md`: a metric behind a mocked adapter needs a numeric test of its
  own; "Pending in-game verification" in a shipped doc is an open defect.
- `docs/reviews/lessons/harmony-il.md`: a postfix on an order setter sees the engine's housekeeping;
  enumerate the callers and tell a re-issue from a new order with a prefix-captured previous value.

## Owed

In-game smoke per the feature doc's checklist. The toggle stays OFF by default until it passes.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/smart-cavalry-ai.md](../features/smart-cavalry-ai.md)

<!-- backlinks-end -->

# RCA — Enlistment content/equipment/duties + FieldCommission (checkpoint 2, 2026-08-05)

Second review cycle of the #375/#376 native rewrite, covering everything built after the
core checkpoint (`25a3340c`): the content layer (config, wages, promotion, merit, rhythm),
the equipment pipeline, the duty engine, the FieldCommission port, and the cross-cutting
attribution fixes. Two review agents (standards, cross-system data flow) plus one
self-caught defect. **9 findings, all fixed in-session.** Final state: full suite 5,415
passing, `validate_moduledata.py` PASS, every entry point within ADR-002.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | **HIGH** | `ServiceRewardService.PayDailyWage` paid `ArrearsReleased` through BOTH the commander transfer and the mint whenever `PayFromCommanderGold=false` — player received ~1.67× the amount owed and the commander was drained gold the config said not to touch | wage accounting | The pure `WagePolicy.ComputeDaily` was correct and fully unit-tested; the ORCHESTRATION that spends its output had **zero tests**. The consumer inferred the payment channel from `Minted > 0` instead of reading the config flag that produced it | Rewrote on conservation accounting (owed − delivered = new debt), branch explicitly on `PayFromCommanderGold`; added `ServiceRewardServiceTests` (12 tests) covering the orchestration, incl. a regression test per bug |
| 2 | MED | The same method double-counted a partial-transfer shortfall: it added the shortfall to `NewlyDeferred` AND subtracted it from `ArrearsReleased`, overstating the debt by exactly the shortfall | wage accounting | Defensive path (unreachable in single-player), so no test and no play-testing would ever hit it. Patching the plan's fields after the fact is inherently error-prone | Same conservation rewrite makes it unrepresentable — the ledger is derived from what actually moved, never patched |
| 3 | MED | `ServiceAssignment` had **no producer**: 3 of 4 values were dead, and the `AssignmentAffinity` gates in the duty data could never match | missing deliverable | Built the consumers (XP routing, duty gates) and the persisted field, but never the flow that sets it. Nothing failed — the default (Infantry) is valid, so it looked complete | Added `IAssignmentService` (cooldown + trust cost) + a reassignment dialog; consumes the two previously-dead config knobs |
| 4 | MED | `EnlistmentBattlePayoutService` never evaluated promotion, despite the design's "exactly two evaluation points" claim — a battle earning the last XP wouldn't promote until the next daily rollover | design/code drift | **Self-caught** while pre-reading my own code against a review question. The claim lived in a doc comment; nothing tested it | Added `IPromotionService` chokepoint used by BOTH points (single code path, no drift); tests assert battle-XP-crosses-threshold promotes immediately |
| 5 | MED | `EnlistmentContentBehavior` hit 171 lines (ADR-002 ceiling 150), with discharge-consequence policy inline | standards | Grew past the ceiling incrementally across three separate additions; no per-edit line check | Extracted `IDischargeConsequenceService`; later re-split duty routing into `EnlistmentDutyBehavior` and the reassign chain into `EnlistmentAssignmentDialogBehavior` when the count crept again |
| 6 | LOW | 4 `LordConversations` prefixes had no co-op disposition — the suite's own governance gate failed the build | co-op governance | Wrote the patches in checkpoint 1 without registering them in `CoopVetoClassificationTests`. **The test caught it, which is the system working** | Registered all four as `ReviewedSafe` with reasons (local dialogue, per-peer by design) |
| 7 | LOW | `MeritScoringConfig.RoleFitBonus` dead: the only producer of `MeritSample.RoleFit` hardcodes `false` | unconsumed data | Deliberately deferred ("later refinement") but left a config knob that silently does nothing | Documented as a known limitation in the feature doc rather than silently shipping a live-looking knob |
| 8 | LOW | `DischargeReason.Commission` / `ContractNotRenewed` fully handled by the consumer but never produced | unconsumed enum | Contract expiry raised a notification only; the leave flow classified everything past the term as `PlayerRequest` | Documented as content-phase reserved (consequences are identical today); noted for the commission-retirement flow |
| 9 | LOW | `EquipmentIssueResult` collapsed 4 distinct failures into one "you already drew your kit" line, masking real content bugs (`NoRosterFound`) from QA | UX/diagnostics | Wired the happy path plus one generic refusal; the distinct results existed but weren't surfaced | Logged per-result at the issuance site; distinct player lines noted for the tuning pass |

## Root-cause patterns

1. **Pure function tested, orchestration untested (findings 1 + 2).** Both wage bugs lived in
   the ~20 lines that *spend* a well-tested pure function's output. The pure/impure split
   is good architecture, but the test discipline followed the purity boundary instead of
   the risk boundary — and money movement is exactly where the risk is. Rule extracted:
   *a service that moves gold, XP, or items gets orchestration tests even when its
   decision logic is a separately-tested pure function.*
2. **Consumers built before producers (findings 3, 7, 8).** Three separate cases of wiring
   the read side of a value while the write side stayed unbuilt. Individually defensible
   (phased build), collectively a pattern: nothing fails, tests pass, and the feature
   quietly can't reach most of its own states. The data-flow agent catches these precisely
   because it traces declaration → consumption in both directions.
3. **Entry points creep past ADR-002 by accretion (finding 5).** Two separate breaches in
   one checkpoint, both from adding "just one more handler." Line count is checked at
   review, not at edit time.

## Why the agents caught (or missed) these

- The **data-flow agent** found 1, 2, 3, 7, 8, 9 — every one required tracing across files
  (config → consumer, plan → spender, enum → producers). No per-file review would surface
  them. It also correctly *cleared* several suspicions I had seeded (the merit-state timing
  and the promotion claim in the daily path were both fine), which is as valuable as the
  finds.
- The **standards agent** found 5 mechanically and confirmed 18/18 adapters and 21 IoC
  registrations clean.
- **Finding 4 was self-caught**, not agent-caught: I read my own battle-payout code against
  a review question instead of assuming the design comment was true. The doc comment
  asserting "two evaluation points" was written before the second point existed.
- **Finding 6 was caught by the existing test suite**, not by any reviewer — the co-op
  disposition registry is a governance gate that fails the build on unclassified prefixes.
  It worked exactly as designed.

## Lessons to codify

- **Orchestration tests for value-moving services** (finding 1's rule above) — appended to
  `docs/reviews/lessons/gamemodels-services.md`.
- **Deriving state from conservation beats patching a plan.** The rewritten `PayDailyWage`
  computes the new debt from *what actually moved*, so a partial transfer, a config flip,
  or a future third payment channel can't desync the ledger. Prefer recomputing an
  invariant to incrementally adjusting the fields that feed it.

## Codex adversarial pass (same day, after the fixes above)

Verdict: **0 P1, 4 P2, 0 P3** — "not safe to put in front of players before live-game
testing," which matches our own position. All four verified against the code and fixed;
each carries a regression test in `CodexFindingRegressionTests` or the duty suite.

| # | Sev | Bug | Why the internal review missed it |
|---|-----|-----|-----------------------------------|
| C1 | P2 | Honorable discharge **erased the arrears it was meant to settle**: `DischargeService` resets the core record BEFORE raising `EnlistmentEnded`, so the consequence layer's final-settlement `Grant` read a null `EnlistedHeroId` and `GoldGiftAdapter` silently no-oped on the failed hero lookup | The internal pass re-derived the wage arithmetic but stopped at the service boundary — it never followed the discharge event ORDERING down into the concrete gold adapter. Cross-service ordering is invisible to both per-file review and per-service tests |
| C2 | P2 | `DeliverFood` duties **completed for free**: `CountPlayerFood` returned `ItemRoster.TotalFood`, which folds in livestock via `item.HorseComponent.MeatCount` (ItemRoster.cs:452), while `ConsumePlayerFood` only removed `IsFood` stacks. A player driving cattle satisfied the check and handed over nothing | Symmetrical-looking adapter names ("count"/"consume") hid an asymmetric engine property. Nobody decompiled `TotalFood` — the internal review verified the APIs it *called*, not the semantics of the value one *returned* |
| C3 | P2 | The wait-menu leave option had **no co-op authority gate**, unlike every other discharge path — a client could clear its own service while the host stayed enlisted | Grep-level authority checks look clean because the ticks and dialogues are all gated; a menu-option *consequence* is a world-mutating entry point that doesn't pattern-match as one |
| C4 | P2 | **NaN campaign days failed open in the duty scheduler**: cooldown comparisons went false (offering duties through the cooldown) and the expiry check went false forever (stranding a duty and its spawned party permanently) | Our NaN sweep covered the high-profile services named in the review prompt; the duty engine arrived from a parallel agent after that sweep and inherited no equivalent audit |

**Pattern across C1-C4:** every one lives at a *seam* — between two services (C1), between an
adapter's name and the engine's semantics (C2), between a UI entry point and the mutation it
triggers (C3), or between a subsystem that got audited and one that arrived later (C4). None
would be caught by reading any single file carefully. The internal data-flow agent found the
same class of bug within the code it was pointed at; Codex's value was arriving with no
knowledge of which parts had "already been reviewed."

**Lesson (added to `docs/reviews/lessons/adapters-taleworlds-api.md`):** when an adapter
exposes a count/consume or read/write pair, decompile BOTH engine members and prove they
range over the same set. A count that is a superset of what the writer can act on is a
silent free-completion bug.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->

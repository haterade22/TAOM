# RCA: alignment-aware execution fall-through deep review (2026-09-06)

**Feature:** Alignment-Aware Execution, issue [#556](https://github.com/haterade22/TAOM/issues/556).
**Review:** 5 agents (standards, engine-API compatibility, efficiency, completeness, cross-system data flow).
**Outcome:** 1 HIGH finding (self-inflicted, introduced by the fix under review), 1 MED doc-staleness, 2 disputed findings rejected with evidence, 2 INFO items documented. HIGH fixed in session with a regression test proven to fail without the fix. Full suite 8380 passed, 3 skipped, 1 pre-existing unrelated failure (`ShippedCultures_EveryBannerBearerReplacementWeaponIsOneHanded`, Armory module not fully installed on this machine; the user confirmed it is expected here).

## The original defect

`ExecutionRelationService` returned the full vanilla execution penalty whenever any of the executor, victim or evaluator kingdom ids was null or empty. Vanilla's penalty chain ends in -10 to every clan leader in the world whose Honor is above 0, which in TAOM is the entire Free Peoples, so "defer to vanilla" meant "charge every ally". Reachable two ways: a kingdom-less executor (independent, mercenary, or enlisted player, since enlistment deliberately does not join the commander's kingdom), and a victim whose `Clan.Kingdom` was nulled by the clan destruction `ApplyInternal` performs at L133-143 before firing `OnHeroKilled` at L144.

The fix added `IAlignmentService.ResolveSide(kingdomId, cultureId)` (kingdom first, culture fallback), deleted the null bail, and snapshotted victim and executor identity in the `ApplyInternal` prefix.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | The `[HarmonyFinalizer]` on `KillCharacterAction_ApplyInternal_Patch` called `ExecutionContext.Clear()` unconditionally. `ApplyInternal` re-enters itself: destroying the victim's clan calls `KillCharacterAction.ApplyByRemove` for every other living hero in it (`DestroyClanAction.cs:43`), and each nested kill runs the same prefix and finalizer while the outer execution is still on the stack. The nested prefix correctly declines to `Set` (wrong detail), but the nested finalizer still cleared, wiping the snapshot before the outer call reached `OnHeroKilled`. The class doc comment asserted "The snapshot is taken before either happens, so it survives both", which was false for any executed lord with a surviving spouse, child or companion. | Lifecycle / re-entrancy | I reasoned about the ORDER of operations inside `ApplyInternal` (destruction before the relation pass) because that was the bug I was fixing, and never asked whether `ApplyInternal` could appear on its own stack twice. The prefix was written re-entrancy-aware (it guards on detail); the finalizer was not, because a finalizer looks like cleanup rather than a decision. | Ownership moved into `ExecutionContext.TrySet` / `ClearIfOwned`, threaded through Harmony's `__state`. New `ExecutionContextTests` (7 tests) pinning the nesting contract. New lessons entry below. |
| 2 | MED | `docs/features/execution.md` still described the pre-fix shape in seven places: the architecture diagram, the `ExecutionContext` description, the model's routing sentence, the `IOnExecutionAction` method list, the consumer list, two test counts, and the "how to extend" steps. | Doc accuracy | I updated `alignment-aware-execution.md` thoroughly because that is the doc the fix is *about*, and treated `execution.md` as the shallower sibling needing only the config table. It was in fact the doc carrying the wiring description the deletions invalidated. | All seven corrected. Covered by the existing lessons entry on prose evidence. |
| 3 | rejected | Standards agent rated `IPlayerContextAdapter` being registered in `SiegeDefenseIoC` rather than core services as CRITICAL. | False positive (severity) | n/a | Rejected. The adapter resolves fine: `SiegeDefenseIoC.RegisterSiegeDefenseFeature` is called unconditionally from `IoC.cs:123` and DryIoc resolves lazily at `OnGameStart`. Pre-existing, not introduced or deepened by this changeset, and `Main/IoC.cs` is single-owner and currently carries another session's uncommitted edit. |
| 4 | rejected | Completeness agent recommended registering `IPlayerContextAdapter` in `ExecutionIoC` to make the feature self-contained. | Actively harmful | n/a | Rejected with evidence: `WarOfTheRingMomentumIoC` carries the comment "IPlayerContextAdapter and IAllianceAdapter are registered by SiegeDefenseIoC / DiplomacyIoC, do not re-register (DryIoc appends duplicates)". Following the recommendation would add the exact duplicate the codebase warns against. |
| 5 | INFO | Executing a Neutral-aligned lord (Khand, Umbar, Shaghana, Abanissa) costs nothing from anyone, including that lord's own faction, and docks no Honor. This falls out of "Neutral is nobody's ally", not from a stated design decision. | Design statement | The predicate definitions produce it; no document asserted it. | The user was offered a change here during planning and chose to leave classifications alone, so it is intended. Now stated explicitly in `alignment-aware-execution.md` rather than left as an emergent property. |
| 6 | INFO | The Execution alignment rules have no MCM toggle, unlike all four sibling alignment features. | Convention gap | Not a defect; vanilla's execution penalties were never gated either. | Surfaced to the user, no action taken. |

Also recorded: the completeness agent reported 74 test methods. The real count is 61 (35 + 5 + 18 + 3), verified by counting `[TestMethod]` and cross-checked against the runner. A confident agent number was wrong; the docs carry the counted figure.

## Root-cause pattern: a Harmony finalizer is a decision, not cleanup

Finding 1 is worth reading as a general shape. The prefix and the finalizer of the same patch were written with different levels of care, and the asymmetry was invisible because of what each one *looks* like:

- The prefix reads like a decision. It takes the target's arguments, branches on `actionDetail`, and obviously has to answer "is this call one of mine?". It got that right.
- The finalizer reads like cleanup. It takes no arguments, does one thing, and answers no question at all. So it never got asked "is this call one of mine?", even though it is running against the same re-entrant target and touching the same shared static state.

Whenever a patch pairs a prefix that **conditionally** writes shared state with a finalizer or postfix that **unconditionally** clears it, the two are already out of step, and the bug only needs the target to re-enter. The engine re-enters far more often than it looks: `KillCharacterAction`, `DestroyClanAction`, `ChangeKingdomAction` and `DestroyKingdomAction` form a mutually recursive cluster, and any of them can appear twice on one stack.

The general duty: **before writing shared state from a patch, establish whether the patched method can appear on its own stack twice, and make the write and the clear agree on ownership.** Harmony provides `__state` precisely for this, and it is per-invocation, so it is the right tool rather than a depth counter.

Note also what nearly hid this. The culture fallback added by the same fix meant the wiped snapshot still produced the *right answer*, because `Hero.Culture` survives clan destruction and every current `alignment.json` kingdom and its matching culture resolve to the same side. Two independent halves of one fix accidentally covered for each other. That is not safety, it is a coincidence that a future kingdom/culture split would end, silently and with no test failing.

## Why each agent missed, or caught, these

- **Standards (haiku):** passed the patch. Its `ExecutionContext` check asked whether Set and Clear are paired and whether the finalizer runs on exception paths, both of which are true. It had no rule about re-entrancy of the patched target, so the pairing looked correct. Produced finding 3 as a false-positive CRITICAL.
- **Compatibility (sonnet):** verified 10/10 signatures and independently confirmed the `ApplyInternal` ordering and the `ApplyByDeathMark` re-entry. It answered the ordering question correctly and did not treat re-entrancy as in scope, which it was not asked about.
- **Efficiency (haiku):** clean, correctly calibrated to the real call frequency, correctly reasoned that struct parameters to interface methods do not box.
- **Completeness (haiku):** caught finding 2, though it reported only one of the seven stale places. Its test count was wrong and its IoC recommendation was harmful.
- **Data flow (sonnet):** caught finding 1, with the full call chain quoted. It was explicitly asked to trace re-entrancy, because the orchestrator had flagged that risk in advance. **This is the honest caveat: the agent found it because the prompt named it.** The generic Agent 5 rule set has lifecycle and observation-state-machine checks but nothing that asks "can the patched method appear on its own stack twice", so an unprompted run would plausibly have missed it too.

## Lessons to codify

One new entry, in `docs/reviews/lessons/harmony-il.md`: a patch that writes shared state must gate its clear on ownership when the target can re-enter. Added there rather than in `state-lifecycle-save.md` because the trigger is a Harmony patch shape (`[HarmonyPrefix]` + `[HarmonyFinalizer]` over a re-entrant engine method), and that is where a future author of such a patch will look.

The deep-review Agent 5 prompt should also gain a re-entrancy question so this does not depend on the orchestrator suspecting it. Recorded here as a follow-up rather than done in this commit, so the harness change is reviewable on its own.

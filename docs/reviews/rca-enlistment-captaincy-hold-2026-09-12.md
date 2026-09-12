# RCA: the enlisted soldier's captain slot (#576) and the parked-out-of-battle detach (#577)

**Date:** 2026-09-12 · **Feature:** Enlistment (deployment, battle roles, reconciler, load normalizer) · **Issues:** [#576](https://github.com/haterade22/TAOM/issues/576), [#577](https://github.com/haterade22/TAOM/issues/577), follow-up [#578](https://github.com/haterade22/TAOM/issues/578)
**Review:** `/deep-review`, 5 agents (standards, compatibility, efficiency, completeness, cross-system data flow). 2 HIGH (one mechanism), 1 MEDIUM deferred, 2 LOW, all verified against source by the author before acting.
**Suite after fixes:** `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` -> 8716 passed, 0 failed, 2 skipped; `lint_docs.py` clean apart from the pre-existing CLAUDE.md size warning.

## Top line

Two player reports, one cause. The 2026-08-12 army join (`c177fe61`, closing #443) flipped
`MapEvent.IsPlayerSergeant()` to fix which team the enlisted player spawns on, and audited that
flag through the one consumer the change was aimed at. Its second consumer,
`SandboxBattleInitializationModel.CanPlayerSideDeployWithOrderOfBattleAux`, opened the Order of
Battle screen at every rank; the sergeant-choice view auto-selected the player as captain;
`AssignSergeant` set `Formation.PlayerOwner`, whose setter turns the formation's AI off, and
TAOM's neither-role pair refused the order UI. Nobody could command that formation. The feature
doc predicted this in writing as "a consequence to watch in-game"; #443 closed without the look.

The matching detach had a window too. `Assess` admitted `EnlistedBattle` and fell through to
`AttachRequired` in the loot and aftermath window, and the reconciler parked the party with no
encounter guard; the park clears `AttachedTo`, which pulls an active party off its `MapEventSide`,
and "Send troops" then indexes `BattleSimulation.SelectedTroops[-1]`. TAOM's own army disband
raised the "army left" edge that re-entered the reconciler from inside the join or inside
`PlayerEncounter.Finish`, and x64 fast-forward ran that pass about 16 times more often than 4x.

**The most valuable finding came from the review, not the plan.** The plan called the save
coercion a pre-existing open question and deferred it. The data-flow agent showed it defeats
every gate the fix added: `EnlistmentRecord` writes `EnlistedBattle` as `EnlistedAttached` on the
stated grounds that battle reality is re-derived at load, and that re-derivation had never been
written. It now exists.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH | After a save at the encounter menu or in the aftermath, the record reloads as `EnlistedAttached`, so the deployment-screen model, the role strip, the placement and the new presence hold all defer or fall through: #576 reopens for that battle and #577's park runs via reload | Persisted-state proxy for an engine fact | The plan filed it as "pre-existing, unverified" and moved on; the author read the 2026-08-08 lesson naming this exact class ("coercing a transient state at save time silently breaks its own re-derivation contract") and still keyed four new consumers on the coerced state. Repeat offender | Written the re-derivation: `EnlistmentLoadNormalizer` restores `EnlistedBattle` at load when the party is in a map event or its battle encounter is open. Rule: a load-time matrix row for every state the persistence layer coerces |
| F2 | MED | MCM master switch off mid-battle: `ReconcileCore` runs the discharge before `Assess`, and `DischargeService` detaches before the ownership policy closes the encounter | Precedence path that bypasses the new hold | The hold was placed inside `Assess`; the discharge runs above it by design ("discharge outranks ownership") and the review enumerated presence writers by path, which the author had not | Deferred and recorded on #577; a design call on whether the switch should wait for the encounter |
| F3 | LOW | A failed join's rollback can leave the encounter open under R1b while `ReassertServiceMenu` re-inits the wait menu, whose init parks unconditionally on `EnlistedAttached` | Second presence writer with no `Assess` | Same enumeration gap as F2 | Deferred and recorded on #577 |
| F4 | LOW | Two comments and the feature doc said moving the player out of a formation he captains clears the captaincy through `RemoveUnit`; `RemoveUnit` clears it only when `!CanLeadFormationsRemotely`, and vanilla sets that flag true on the player in the general's-formation path | Doc claim wider than the decompiled condition | The author read `Formation.cs:2195` for the null write and not the `if` two lines above it | Text corrected; the explicit clear was already load-bearing, so no behaviour change. Rule: quote the condition, not the assignment |
| F5 | LOW | `Mission.MainAgent` non-null at `OnAfterDeploymentFinished` was asserted, not proven | Unproven ordering claim in a comment | The compat agent rated it Likely and asked for a smoke | Proven from `FinishDeployment:72-74` (controller set, which assigns `MainAgent`) before `:78`; documented in the comment |

Also fixed under the same issues, found by the pre-implementation exploration rather than the
review: `docs/features/enlistment.md` had carried "the Order of Battle screen is unreachable while
enlisted, and always was" under a "do not re-derive" banner for a month after the premise changed,
900 lines away from the paragraph that predicted the bug.

## Root-cause pattern: a state that is a proxy for an engine fact, across a persistence boundary

F1, F2 and F3 are one shape. `EnlistedBattle` stands in for "the player is in his commander's
battle", and it is a good proxy inside one session because the join writes it before anything
else runs. It is not the engine's own fact, and the persistence layer does not carry it. Every
consumer that keys on it inherits both weaknesses, and this change added four consumers.

The feature had already learned this once, on the army-leave guard (`noBattleAnywhere && IsInArmy`
instead of `state == EnlistedBattle`, "found by the Codex pass, 2026-08-12"), and the lessons file
already carried the rule (`state-lifecycle-save.md`, 2026-08-08). The author read that lesson
before starting and still keyed the new gates on the state, because the rule was filed under
"save" and the work was filed under "battle". The re-derivation closes the gap for every consumer
at once, which is why it is the fix rather than four engine-fact reads.

## Why each agent missed what it missed

- **Standards** (haiku): file-scoped rules; the defect is between `EnlistmentRecord` and four
  consumers. Nothing in its rule set asks what persists.
- **Compatibility** (sonnet): found F4 and F5, which are its kind of finding (a decompiled
  condition and an ordering proof). It does not trace TAOM state.
- **Efficiency** (haiku): nothing here allocates on a hot path; correctly silent.
- **Completeness** (haiku): counts artefacts; found nothing missing because nothing was.
- **Data flow** (sonnet): found F1 to F3 by enumerating every presence writer and every consumer
  of the state, which is the review the author had not done. Its severity on F1 (HIGH) stands;
  the trigger is narrow (a save in one menu) but the effect is the exact user-facing bug.

## Process note

Work item 2's RED was not observed as a separate run: the tests and the production code were
written in consecutive turns and built together. The tests' assertions (no park on the aftermath
shape, the own-mutation guard both ways) are the kind that fail against the old code, but that
was not demonstrated. Recorded so the next reader does not take the green run as proof of RED.

## Feedback memories to codify

One systemic lesson, appended to `docs/reviews/lessons/campaign-mechanics.md` under the #577
entry: when a gate keys on a TAOM state that the persistence layer coerces, either re-derive the
state at load from the engine's own bits (what was done) or key the gate on those bits directly;
and treat a "consequence to watch in-game" in a doc as an open issue, not a note. The #576 lesson
(enumerate every reader of a flag before flipping it) is in `gamemodels-services.md`.

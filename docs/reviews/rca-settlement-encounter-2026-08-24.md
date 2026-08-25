# RCA: the settlement-encounter invariant (#510, #511)

**Scope.** The `/deep-review` pass over the #510 fix: five agents (standards, API compatibility,
efficiency, completeness, cross-system data flow) over the change that made TAOM stop putting the
main party inside a settlement without a `PlayerEncounter`. Three findings were regressions the fix
itself introduced; one was a third instance of the original bug class, deferred to #511 with the
engine evidence. Every finding below was re-verified against the installed v1.4.8 decompile before
being entered here, per `.claude/rules/evidence-over-claims.md`.

**A second round followed**: `/review-codex` on the fixed changeset, then nine adversarial verifiers
and a completeness critic over its findings. That round is in "The Codex round" below, and it
corrected two things this first section originally got wrong. Read both.

**Fix state.** Findings 2, 3 and 4 fixed in the same changeset. Finding 5 deferred to #511 with a
written rationale (two candidate fixes, both needing an in-game smoke, one of them colliding with
the battle path). Findings 1a and 1b are the original bug, fixed.

---

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1a | CRITICAL | Discharge released the player into the commander's town on the raw vanilla `town` menu with no `PlayerEncounter`; `game_menu_settlement_wait_on_init` derefs `PlayerEncounter.EncounterSettlement` unguarded | State/lifecycle | The invalid state was masked by `RedirectMenuIds`, and the discharge clears the record to `NotEnlisted` (which the redirect is gated on) BEFORE placing the player, so the mask was off exactly where it was needed | `EnsureSettlementEncounter` chokepoint plus the `SettlementEncounterInvariantTests` IL ban |
| 1b | CRITICAL | Shore leave released `town`/`castle`/`village` to a still-enlisted player with the same null encounter, additionally reaching the tavern, arena and town-centre walk (all `LocationEncounter` derefs) | State/lifecycle | Shore leave was written against a feature-doc paragraph that NAMED the `LocationEncounter` requirement, and shipped without it | Same chokepoint; the doc paragraph is now corrected rather than merely present |
| 2 | MED | The fix's own shore-leave revoke called `Finish(false)` while the player could still be inside the settlement, taking their menu and stopping time without walking them out | State/lifecycle | I reasoned "the revoke fires after the column marched, so the player is already outside" and never enumerated the other trigger: `ShouldRevokeLeave` also fires on a STATE change while the player is still standing in the town | `Finish(...)` with the force flag, plus the regression test `Pump_ShoreLeaveRevokedWhileStillInsideTheSettlement_ForcesThePlayerOut`. **The first fix used the WRONG predicate and the Codex round caught it, see finding 7** |
| 3 | LOW/MED | Refusing a shore-leave pass became a silently dead button | Localization/UI | The refusal path was unreachable before this change (the option condition had already ruled out every failure), so the pre-existing bare `return` was dead code that the change quietly brought to life | Toast via the presenter's existing `_inquiry.ShowMessage` pattern; new `{=taom_enlist_leave_refused}` key |
| 4 | LOW | The new ban test's allow-list justified `FollowCommanderIntoSettlement` with "no vanilla settlement menu is ever reachable from it", which shore leave makes false | Testing/QA | I wrote the justification from the follow path's own doc comment without re-checking it against the feature that had been added since | The comment now names the real guarantor (`TakeTownLeave`'s own chokepoint call) instead |
| 5 | HIGH | The redirect mask is gated on `EnlistedAttached` alone, so it disarms entirely in `CommanderUnavailable` while the player is inside a settlement; `Campaign.Tick` can then push `town_outside` (**the failure shape first written here was wrong, see finding 6**) | State/lifecycle | The #510 fix targeted the two sites that had produced evidence (a crash bundle, a shipped feature). Nobody asked the general question the invariant implies: for which OTHER states is the mask off while the player is inside a settlement? | Deferred to #511 with the `Campaign.Tick` and `GetGenericStateMenu` evidence, and with both candidate fixes and their risks written down |

---

## Root-cause class: a mask is not a fix, and the masked state is one un-masking away

Findings 1a, 1b and 5 are one bug with three doors. TAOM created a state the engine never creates
(main party inside a settlement, no `PlayerEncounter`, no `LocationEncounter`) and made it
survivable by suppressing the vanilla UI that would have touched it. Suppression is not a property
of the state, it is a property of one code path, and three separate paths turned it off: the
discharge by clearing the record, shore leave on purpose, and `CommanderUnavailable` by leaving the
one state the gate names.

**The tell that this class is present:** you are suppressing vanilla UI so that a state stays
survivable. At that moment the invalid state is the bug, not the UI.

**What makes this instance worth an RCA rather than a patch:** the hazard was correctly documented
in two places before it shipped. `ServiceAttachmentService.FollowCommanderIntoSettlement`'s doc
comment named the exact crashing method. `docs/features/enlistment.md` said letting the player use
the town needed the `LocationEncounter` work first. Shore leave was then authored against that same
document and shipped without it, one month later. **Prose adjacent to the code did not hold the
line, and there is no reason to expect it to next time.** That is why the fix ships an IL-scanning
ban test (`SettlementEncounterInvariantTests`) rather than a stronger comment. The ban caught
`DischargeService.RestoreCampaignContext` on its first run, before any human read the diff.

## Secondary class: a fix that makes a dead path live inherits that path's defects

Findings 2 and 3 are both this. `TakeTownLeave` had a bare `return` on refusal that was dead code
because nothing could refuse; adding a real refusal reason resurrected it as a silent button. The
revoke's `Finish` argument was written for the only trigger I had in mind, and the second trigger
made the wrong argument reachable.

**Prevent:** when a change introduces a new failure return into an existing method, walk every
caller and ask what it does with that value TODAY, not what it was designed to do. When a change
adds a second trigger to an existing branch, re-derive that branch's arguments from the union of
triggers rather than from the original one.

---

## Why each agent missed what it missed

- **Standards (Agent 1)** passed correctly and had nothing to say about any of these: every finding
  is behavioural, not architectural. It did usefully confirm the `IlCallScanner` extraction was
  behaviour-preserving, which is the one thing in the diff that could have silently weakened an
  existing gate.
- **API compatibility (Agent 2)** verified 13 signatures and answered the three correctness
  questions, and it is the reason the save/reload question is settled rather than assumed. It could
  not have found findings 2 or 5: both are TAOM control flow, not engine signatures.
- **Efficiency (Agent 3)** correctly scoped itself out. Nothing here is a performance question.
- **Completeness (Agent 4)** passed all eight checks and made one call worth keeping: the adapter's
  success-path branches have no unit coverage and are exercisable only in-game. That is correct per
  ADR-008, and it is why the smoke list is load-bearing rather than ceremonial.
- **Data flow (Agent 5)** found 2, 3, 4 and 5. It is once again the only agent that found anything,
  which matches the standing note in the skill that every HIGH bug Codex has caught in this project
  was a cross-system data-flow gap. Two of its findings rested on engine facts it could not
  decompile in-session, and it correctly reported them as unproven rather than asserting them; the
  orchestrator then decompiled `Campaign.Tick` and `PlayerEncounter.Finish` and both held up.

**The reviewer-side lesson:** an agent that flags a finding as "structurally plausible, trigger
unproven" is doing the right thing, and that finding is not downgradeable on the strength of the
missing half. Both of Agent 5's unproven-trigger findings were real once the engine was actually
read. Treat "I could not decompile X" as a work item for the orchestrator, not as a discount.

---

## The Codex round (added after the deep-review section above)

`/review-codex` ran on the fixed changeset and returned 3 P1, 5 P2, 1 P3. Nine adversarial verifiers
plus a completeness critic then re-tested every one against the installed v1.4.8 decompile.

**All three P1s collapsed.** That is the headline, and it is the argument for keeping the verify
step rather than auto-implementing a reviewer's severities.

| Codex | Claim | Verified verdict |
|---|---|---|
| P1 | `EnsureSettlementEncounter` accepts a siege battle encounter as a settlement encounter and freezes the battle | **DISPUTED.** The pre-fix code reached the same state (`MoveIntoSettlement` short-circuits when already inside, and the untouched `EnsureMenuOpen` half switched the menu either way), so it is not a regression. The battle is not frozen: the `town`/`castle` menu carries `town_leave`, whose consequence is `PlayerEncounter.LeaveSettlement(); Finish();` and `Finish` runs `FinalizeBattle` to `LeaveBattle`. Codex's implied fix is actively worse: refusing would route a siege DEFENDER into `LeaveSettlementAction`, and `MapEvent.AddInvolvedPartyInternal` rewrites a siege assault to SiegeOutside for a defender with no `CurrentSettlement` |
| P1 | Shore leave permanently blocks a commander battle join | **DISPUTED.** The quoted guard is a comment, not code; the real one in `EncounterAdapter` returns true early when the live encounter already matches the map event |
| P1 | #511 is a ship blocker | **PARTIAL, no code change.** The premises hold but the chain breaks at `Campaign.Tick`'s `AtMenu == false` gate, and nothing in that state closes the TAOM wait menu. More importantly the failure SHAPE was wrong in four artifacts, ours included, see finding 6 below |
| P2 | IL ban evadable | **PARTIAL.** Real, fixed |
| P2 | `PlayerPartyInitialStrength` left at zero | **PARTIAL.** Real, one line, fixed |
| P2 | Failure is not atomic | **PARTIAL.** Real, fixed |
| P2 | Discharge menu failure orphans the encounter | **CONFIRMED P2.** The only finding that survived at its stated severity. Fixed |
| P2 | Shore-leave grant not transactional | **DISPUTED** |
| P3 | `ReadFailed` never consulted | **PARTIAL.** Documentation drift, comment corrected |

The completeness critic then found five more, all P3, of which three were taken.

## Second-round findings

| # | Sev | Bug | Why missed | Preventive action |
|---|-----|-----|-----------|-------------------|
| 6 | MED | **We wrote a wrong engine fact and copied it into four artifacts.** The claim that `town_outside`'s Leave option no-ops on a null `Current`, producing a soft-lock. `game_menu_town_outside_on_init` derefs `PlayerEncounter.EncounterSettlement.Name` first, so the menu NREs at init: a CTD, not a soft-lock | The claim was inherited from a pre-existing lessons entry and a `DischargeService` comment and repeated without decompiling the `on_init`. A statement already written down in the repo read as established fact | Corrected in all four places, each with a dated correction note rather than a silent edit. **The lesson: an in-repo claim is not evidence. `evidence-over-claims` C applies to our own prior prose, not only to agents and reviewers** |
| 7 | MED | The revoke's force flag used `PlayerPresenceFlags.IsInSettlement`, which is only the settlement id. Vanilla's `PlayerEncounter.InsideSettlement` also requires `MainParty.IsActive`, and enlistment parks the party inactive WITHOUT leaving the settlement, so the two disagree and `Finish(true)` silently no-ops the walk-out | I quoted vanilla's guard correctly in the comment and then compared against the wrong predicate. `IEncounterAdapter.IsInsideSettlement` already wrapped the right one and had zero consumers | Now `Finish(_encounter.IsInsideSettlement)`, with a regression test pinning the divergent shape |
| 8 | LOW | `OnConversationEnded` re-asserts the service menu with no `OnTownLeave` check, unlike `EnsureServiceMenu`, so any conversation ending on a pass slams the TAOM menu over the town the player is entitled to, and the pass's own option is then hidden by `!alreadyOnLeave` | Pre-existing, but the fix made it load-bearing by putting a live encounter behind that menu | One-line guard matching the sibling carve-out |
| 9 | LOW | Two tests passed against both the old and the new implementation | I asserted the new call happened without asserting the old one did not, and one predicate was vacuously true when placement never touched the adapter | Both now assert the discriminating fact. Codex's test-sensitivity table is the technique: for each test, state what it would do against the reverted code |

## What the second round says about the first

The deep-review agents and Codex found disjoint sets. Codex's only surviving P2 (the orphaned
encounter on menu-open failure) was in a code path a deep-review agent had read and passed, and the
critic's `IsActive` predicate mismatch was in a line the deep-review round had itself asked me to
write. Neither round was redundant.

**The one technique worth stealing from Codex:** the test-sensitivity table. Asking "what would this
test do against the pre-fix code" caught two worthless tests that a coverage count reports as
coverage. Nothing in the deep-review agent set asks that question.

**And the one habit worth breaking:** three of the four second-round findings on our own work were
introduced by fixes written in response to the first round. Fixes written under review pressure get
less scrutiny than the original code, and this is the second RCA in this file to say so.

## Lessons codified

Appended to `docs/reviews/lessons/state-lifecycle-save.md`: "A redirect list is a MASK over an
invalid state, and every mask is one un-masking away from the crash." No new feedback memory: the
existing `evidence-over-claims` discipline already covers the verify-before-relaying half, and the
mask lesson belongs with the other state-lifecycle rules rather than in the harness memory.

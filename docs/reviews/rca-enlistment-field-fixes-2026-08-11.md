# RCA — enlistment field-test fixes (2026-08-11)

Seven field reports from a live playtest, fixed in one changeset on `feat/enlistment-field-fixes`.
Six of the seven traced to a single decision (`MobileParty.MainParty.Army` kept permanently null).
This is the review record: what the review found, why it was missed, and what stops the category
recurring.

Review: 5 deep-review agents. Suite 6311 baseline → 6380 after the first pass (+69 tests) → **6400
after the second pass** (+89 total; 0 failed, 2 skipped, measured 2026-08-12).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 1 | HIGH | `ServiceDiplomacyService` shipped with **no tests**. `ApplyServiceWars` / `UnwindServiceWars` were unverified, including the snapshot-before-declare ordering that is the sole guard against ServeAsSoldier's universal-peace bug. | Testing | I wrote pure-policy tests for `ServiceWarPolicy` and treated the service as thin glue. It is not: the ORDER in which it snapshots is behaviour only the service can get wrong, and the policy tests are blind to it. | Rule below: "a policy's tests do not cover its service's ordering." 7 tests added. |
| 2 | HIGH | The three new `EnlistmentRecord` fields (`OnTownLeave`, `MirroredWars`, `EnemiesAtOath`) had **no round-trip test**, and no test that a save predating them deserializes to empty lists rather than null. | Save-compat | The existing `Serialize_RoundTrip_PreservesAllFields` test is named "AllFields" but asserts a hand-listed six. Adding a field does not fail it, so it gave false assurance by its name. | 3 tests added (round-trip, legacy-save upgrade, `Reset()` clears). Rule below on "AllFields" tests. |
| 3 | MED | Shore-leave passthrough in `EnlistmentMenuService` had no test — a change to the `OnTownLeave` check would silently regress the town fix with a green suite. | Testing | I tested the pure `TownLeavePolicy` and the record field, but not the seam where they meet the redirect list. | 9 tests added, including the paired negative (without the pass, the menus still redirect) and the narrowness check (approach menus stay redirected). |
| 4 | MED | `EnlistmentContentBehavior` pushed from 153 to 196 lines by inline announcement code (ADR-002 ceiling is 150). | Architecture | The file was ALREADY over at HEAD, so "am I over the limit?" was already true before my edit and read as pre-existing debt rather than something I was making worse. | Extracted to `Presentation/ServiceDailyAnnouncer.cs`; file now 152, below where I found it. Rule below on inherited-violation drift. |
| 5 | LOW | `EnlistmentBattleBehavior` 201 → 215 lines. Same ceiling. | Architecture | Same as #4. Accepted: the growth is the `CommanderBattleMatchPolicy` doc comment, and the file remains a pure router. Recorded rather than fixed. | None — recorded as pre-existing debt. |

### Second pass — the data-flow and API agents (findings 6–11)

Findings 1–5 above were triaged from the first three agents. The API-compatibility and data-flow
agents reported after that triage, and their findings were carried across a session boundary and
resolved on 2026-08-12. One of them was a crash.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 6 | **HIGH — crash** | An army raised by the bare `Army` ctor carries `AiBehaviorObject == null` forever, and `Army.GetLongTermBehaviorTextForAILeadedParty` dereferences it with **no null guard in 5 of its 7 cases** (`GoToSettlement`, `BesiegeSettlement`, `RaidSettlement`, `DefendSettlement`, `PatrolAroundPoint`). `DisbandCreatedArmyIfEmpty` deliberately left such an army standing when other lords had joined it. | API compatibility | I verified that a null `AiBehaviorObject` keeps the army's siege and owner-change handlers INERT — all of which gate on `AiBehaviorObject is Settlement` — and stopped there, writing "stays inert" into the doc comment as though inertness were the whole consequence. I audited what the null value would fail to *do* and never asked what would *read* it. | Disband unconditionally. `ArmyMembershipBindingTests` pins the engine surface + the no-carve-out rule. Lesson below. |
| 7 | MED | `EnlistmentReconciler`'s stale-battle self-heal flipped `EnlistedBattle → EnlistedAttached` but had no `IArmyMembershipAdapter`, so it could not `LeaveArmy()`. | Data flow | I traced army membership along the path that acquires it (`TryJoin` → `OnCommanderBattleEnded`) and confirmed both ends matched. The reconciler is a *third* exit from battle state that I never enumerated, because it is a self-heal rather than part of the design's happy path. | Adapter injected, `LeaveArmy()` called in that branch, 3 tests (leaves when stale, does NOT leave mid-battle, does NOT leave on ordinary parked service). |
| 8 | MED | `ArmyMembershipAdapter._createdArmy` is a live `Army` reference on a `Reuse.Singleton` in a **process**-scoped container, with no reset on load. | Lifecycle | `EnlistmentBehavior.OnGameLoaded` already called `_maintenance.ResetSessionCaches()` — the presence of one reset made the load path look handled. I never asked which OTHER singletons hold campaign-scoped state. | `ResetSessionCaches()` on the adapter, wired beside the maintenance reset; DryIoc validation + a source pin on the call. |
| 9 | MED | `MeritBand.Renown` was dead config. No default band set it, no shipped JSON key existed, so `bandRenown` was always 0 and every battle paid the identical flat base — while `BattleRenownPolicy`'s doc comment claimed "the band figure does the differentiating." | Config / doc drift | `BattleRenownPolicyTests` passes `bandRenown` in as a literal in all six cases. The pure function was completely covered and completely disconnected from the values that reach it, so 100% coverage of the consumer proved nothing about the supply. | Bands populated 3/2/1/0, `renown` + `battleWinRenown`/`battleLossRenown` keys added to the shipped JSON, `Renown` added to `IsValidBandLadder`'s non-negative set, comment rewritten to state the real numbers. 4 tests including one that reads the shipped file. |
| 10 | LOW/MED | `GetDailyWage()` gated only on `IsEnlisted` (five states), but `RunDailyTick` skips `PayDailyWage` when the state is `CommanderUnavailable`. The wallet promised income on days none arrived, for a grace window up to a week. | Data flow | The projection and the payment were written as separate features — one "show the wage", one "pay the wage" — and each was correct against its own spec. Nothing asked whether the two gates named the same condition. | Gate narrowed to match the tick, with a `DataTestMethod` asserting every OTHER enlisted state still projects (so the fix cannot over-correct). |
| 11 | LOW | `TaomClanFinanceModel` overrode `CalculateClanGoldChange` but not `CalculateClanIncome`, so the clan screen's Income tile showed no wage while the expected-change tooltip beside it did. | Completeness | I found the one method that the money actually flows through and stopped. `CalculateClanIncome` calls `CalculateClanIncomeInternal` directly and never routes through `CalculateClanGoldChange` (verified on installed 1.4.8) — a fact only visible by decompiling the base class rather than reasoning about it. | **Fixed, not deferred** (the handover called it probable won't-fix). Its only engine caller is `ClanManagementVM`, always with `applyWithdrawals: false`, so there is no double-pay surface at all; the guard is kept anyway and both overrides now share one `AddServiceWageLine`. |

### Third pass — the Codex adversarial run (finding 12)

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 12 | **P1 / HIGH** | Two service-exit paths could leave a raised army standing. **(a)** `DischargeService` has no `IArmyMembershipAdapter`; it calls `_attachment.ClearArmyAttachment()`, which detaches the PLAYER but knows nothing about the army — and discharge fires mid-battle whenever the MCM master switch is turned off or `CommanderDead` is raised from `EnlistedBattle`. **(b)** `EnlistmentRecord.ToPersistedState` COERCES `EnlistedBattle` to `EnlistedAttached`, so a save taken mid-battle reloads with the player still merged and a state the reconciler's `EnlistedBattle`-keyed self-heal cannot see. | Lifecycle / data flow | Finding #7 fixed the reconciler branch and I treated the exit set as closed, because I had enumerated the paths that end a BATTLE. Discharge ends SERVICE — a different question I never asked, and `DischargeService` was not in the changeset's mental scope because the army work never touched it. The save/load half is worse: I knew about the state coercion (it is documented in `ReconcileRetiredDetachedDuty`'s own comment three screens away) and still wrote a state-keyed guard. | `DischargeService` now takes the adapter and calls `LeaveArmy()` above `ClearArmyAttachment()`, with an ordering test and a per-`DischargeReason` loop test. The reconciler guard was re-keyed from `record.State == EnlistedBattle` to "no battle anywhere AND `IsInArmy`", which is blind to the coercion by construction. 4 tests. |

### Fourth pass — the re-run API and data-flow agents (findings 13–15)

The first API-compatibility and data-flow agents were lost when the session's process exited before
they reported. Re-running them was the highest-value thing left, and it produced the two findings
that mattered most in the whole review.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 13 | **CRITICAL** | Three MORE unguarded readers of `AiBehaviorObject`, beyond the one finding #6 found. The worst: `LordConversationsCampaignBehavior.conversation_lord_tell_objective_gathering_on_condition` reads `Army.AiBehaviorObject.Name` gated ONLY on `Army != null && Army.IsWaitingForArmyMembers()` — no `ArmyType` check, unlike its three sibling conditions — and `IsWaitingForArmyMembers()` returns **true forever** for a bare-ctor army, because `_armyGatheringStartTime` stays 0 and the only writer of it requires `AiBehaviorObject is Settlement`. Also `MobileParty.CheckAiForMapChangeAndUpdateIfNeeded`'s `GoToPoint` case (branches on null, then dereferences on that same branch), and the fact that `SetPartyAiAction`'s `PatrolAroundPoint` branch sets `DefaultBehavior` WITHOUT writing `AiBehaviorObject` — so that text case is reachable with a genuinely unset objective. | API compatibility | Finding #6 grepped `Army.cs` and stopped at the class boundary, because the field is `Army`'s. Two of the three readers are in OTHER types. And I reasoned about which text cases were reachable from `DefaultBehavior` without checking what actually WRITES `DefaultBehavior` — `SetPartyAiAction` pairs the two writes everywhere except the one case that then crashes. | `AiBehaviorObject` is now SEEDED at construction, so the invariant is a property of the object rather than of my ability to enumerate exits. Reader list recorded in the feature doc so it is never re-derived. `ArmyMembershipBindingTests` pins the conversation reader and the seed. |
| 14 | **HIGH** | The war mirror declares as `Hero.MapFaction` resolved LIVE, and `MapFaction` is `Clan.Kingdom ?? Clan` — so a clan that joins a kingdom mid-service makes `UnwindServiceWars` call `MakePeaceAction.Apply` on the KINGDOM, ending a war for every vassal in it because one soldier was discharged. | Data flow | The ordering test (`snapshot before declare`) made the diplomacy path feel thoroughly reviewed, and it IS correct — about ordering. Nobody asked whether the *identity* doing the declaring is the same one doing the undoing. "Is the sequence right?" and "is it still the same actor?" are different questions and only the first was asked. | `EnlistmentRecord.OathFactionId` pins the declaring faction; the unwind refuses under a different one and clears the mirror without acting. 5 tests including the legacy-save path. |
| 15 | MED | `EnlistmentReconciler._lossAnnouncedFor` is never cleared on commander recovery, so a lord captured → ransomed → lost again takes the player into a second grace window in silence. | Lifecycle | The latch's own doc comment reasons carefully about the save/load case and concludes hearing the message twice is the right failure — which reads as "the lifetime question has been considered." It had been, for one episode. The second episode is a different lifetime and the comment's confidence hid that. | Re-armed alongside `GraceEndsAtDay = null` in the recovery branch. |

**The limitation recorded in the third pass is now closed by the seed.** After a save taken
mid-battle the adapter's `_createdArmy` handle is still gone, and such an army is still not
disbanded — but it now carries a real `AiBehaviorObject`, so it is a stray one-lord army rather than
a crash. The reconciler detaches the player from it on the next hourly tick. That is a cosmetic
residue, not a defect worth a save-format change, and finding #13 is precisely why the earlier
"narrow window, accept it" reasoning was wrong: the window was narrow, but what waited inside it was
a guaranteed CTD on the single most likely post-reload action (talking to your commander).

Two self-caught errors during the work, not found by the review but worth the record:

| # | Finding | Why it happened | Prevention |
|---|---------|-----------------|-----------|
| A | Wrote "16 new localization keys" into the CHANGELOG without counting. Actual: 5. | Wrote the artifact from memory of the work instead of from the diff. This is `evidence-over-claims.md` §C trap 1, verbatim. | Counted from `git diff` and corrected before commit. |
| B | A test asserted `village_outside` was in `RedirectMenuIds`. It is not; `castle_outside` is. | Wrote a test from an assumed config shape rather than reading the list. | Read the actual list, corrected the `DataRow`s. The test failing is the system working. |

## Root-cause pattern: pure-policy tests create a false sense of coverage

Findings 1, 2 and 3 are one pattern. I extracted the decisions into pure static policies
(`ServiceWarPolicy`, `TownLeavePolicy`, `BattleRenownPolicy`, `WageReportPolicy`) and tested those
thoroughly — 40+ tests, edge cases, nulls, the SAS-bug regressions. That felt like coverage.

It was not. **A pure policy cannot test the three things that actually break in production:**

1. **Ordering** — `ServiceWarPolicy.FactionsToPeaceOnDischarge` is correct set arithmetic, but the
   universal-peace bug happens if the caller snapshots `EnemiesAtOath` *after* declaring rather than
   before. The policy is agnostic to that and always passes.
2. **Persistence** — a policy operating on `List<string>` never asks whether that list survives a
   save. The serializer is a different file with different failure modes.
3. **Wiring** — `TownLeavePolicy.CanTakeLeave` returning true is worthless if `EnlistmentMenuService`
   does not consult the flag it sets.

Extracting a policy moves the risk from the policy to the seam. The seam is where the tests were
missing, and it is precisely the place a green suite is most reassuring and least informative.

## Why each agent missed what it missed

- **Agent 1 (Standards)** found #4 and #5 and correctly passed everything else. It is not scoped to
  ask whether tests exist, so #1–#3 were out of its remit by design.
- **Agent 2 (API compatibility)** is scoped to TaleWorlds signature verification. Test coverage is
  not its question.
- **Agent 3 (Efficiency)** correctly found nothing and, notably, verified rather than assumed —
  it decompiled to establish which paths were genuinely hot and reported the not-hot ones explicitly.
- **Agent 4 (Completeness)** found #1, #2 and #3. This is the agent that earned its place in this
  review: every finding that changed the code came from it.
- **Agent 5 (Data Flow)** found #7, #10 and #11 — every finding about two pieces of code that each
  worked and disagreed with each other. None of them is a defect in any single file, which is
  exactly why the other four agents passed them.

The lesson for the agent set: Agent 4's value came from asking "does a test exist for this
*behaviour*", not "does a test file exist for this *class*". The three gaps were all in files that
already had test files.

**Codex found what all ten agent-passes missed (#12), and the shape of the miss is the lesson.** Every
Claude agent — including the two whose entire remit was cross-file data flow — traced the paths that
end a BATTLE, because that is the vocabulary the changeset is written in. Codex opened
`DischargeService`, a file the changeset never touched, and asked what happens to the army when
SERVICE ends rather than when the battle does. It also caught that a state-keyed guard is defeated by
a state coercion documented in the same class. Note that Codex labelled its own finding UNVERIFIED —
its `ilspycmd` was sandbox-blocked — so it was confirmed here by reading `DischargeService.cs:79`,
`MobilePartyAttachmentAdapter.cs:225-251` and `EnlistmentRecord.cs:230-243` directly. A finding
labelled unverified by a confident reviewer is still a finding; the label tells you who has to do the
verifying, not whether to take it seriously.

Agent 2's finding #6 deserves its own note, because it inverts the first-pass conclusion about that
agent. In the first pass it was written off as "scoped to signature verification, test coverage is
not its question" — and its 28/28 signature check did indeed find nothing. The crash it caught came
from the OTHER half of its remit: not *does this member exist* but *what does the engine do with the
value we hand it*. That is the question no other agent asks, and it was worth the whole review.

## Lessons to codify

Appended to `docs/reviews/lessons/testing-qa.md`:

### A pure policy's tests do not cover its service's ordering

When a decision is extracted into a pure static policy and a service calls it, the policy tests
cover the arithmetic and nothing else. Write a separate service test for **the order of the calls**
whenever the service reads state, mutates state, and then reads it again — the classic shape being
"snapshot the world, change the world, record what changed."

**Why missed:** 40+ policy tests felt like thorough coverage of the diplomacy feature. The one
behaviour that would destroy a player's campaign (peacing out of wars he started before enlisting)
lives entirely in the two-line ordering inside `ApplyServiceWars`, which no policy test can see.

**Prevent:** for every new `XxxPolicy` + `XxxService` pair, ask "what does the service do BETWEEN the
policy calls?" and test that.

**Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` finding #1.

### A test named `*_PreservesAllFields` must fail when a field is added

A round-trip test asserting a hand-listed set of fields does not cover fields added later, but its
name claims it does — so the next author reads the name, sees green, and ships an unpersisted field.
Save-record fields fail silently and are discovered by players.

**Why missed:** `EnlistmentRecord`'s round-trip test is called `Serialize_RoundTrip_PreservesAllFields`
and asserts seven of the (now) ten fields. Three new fields were added and it stayed green.

**Prevent:** when adding any field to a persisted record, add its round-trip assertion AND a
legacy-save test proving an old save without the key deserializes to a safe default (empty list, not
null — the enlistment discharge path enumerates both new lists unguarded). Consider a
reflection-driven test that enumerates the record's properties so the name becomes true.

**Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` finding #2.

### Inheriting an ADR violation is not licence to deepen it

A file already over the ADR-002 ceiling reads as pre-existing debt, so the check "am I over the
limit?" is already true before the edit and stops discriminating. The question that still works is
**"is this file bigger than I found it?"**

**Why missed:** `EnlistmentContentBehavior` was 153 lines at HEAD (already over 150). Adding 43 lines
of announcement code did not trip any new alarm because the alarm was already ringing.

**Prevent:** for entry points, compare against `git show HEAD:<file> | wc -l`, not against the
ceiling. Leave the file no larger than you found it.

**Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` finding #4.

### When you skip an engine initializer, audit what READS the field it would have set

Constructing an engine object through a bare constructor to avoid an initializer's side effects
leaves every field that initializer would have populated at its default. Auditing that the null
value makes things INERT is only half the audit — inertness is what the field fails to *drive*. The
other half is what *reads* it, and a reader has no reason to guard a field the engine's own
construction path always fills.

**Why missed:** `ArmyMembershipAdapter` deliberately uses `new Army(...)` instead of
`Kingdom.CreateArmy` because `CreateArmy` calls `Gather()`, which would march the commander off the
battlefield. I verified that the resulting null `AiBehaviorObject` keeps the army's siege and
owner-change handlers inert — they all gate on `AiBehaviorObject is Settlement` — and wrote exactly
that into the doc comment. I never grepped for readers. Five cases of
`Army.GetLongTermBehaviorTextForAILeadedParty` cast and dereference it unguarded, reached from the
map party tooltip and the kingdom Armies tab, and `_aiBehaviorObject` is `[SaveableField(16)]`, so
the crash survives every reload. `Army.CheckInactivity` even DECREMENTS the inactivity counter for
besieging leaders, so the army never times out on its own.

**Prevent:** after choosing a bare constructor over an engine factory, list the fields the factory
would have set and grep the engine for every read of each — not just the writes and the gates. If a
reader dereferences without a guard, the object must not outlive the narrow window you created it
for. Pin the finding with a `[TestCategory("BindingVerification")]` test so an engine bump that adds
the guard tells you the workaround can relax.

**Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` finding #6.

### Two gates on the same condition must be written from each other, not from their own specs

A projection ("show the player what they will earn") and an action ("pay them") are naturally built
as separate features with separate specs, and each can be perfectly correct against its own spec
while disagreeing about *when*. The disagreement is invisible to both features' tests, because
neither test suite knows the other gate exists.

**Why missed:** `GetDailyWage()` gated on `IsEnlisted`, which spans five states.
`EnlistmentDailyService.RunDailyTick` skips `PayDailyWage` in one of those five,
`CommanderUnavailable`, for a well-documented reason. The wallet therefore promised income on
exactly the days none arrived — and the wallet tooltip is the one surface a player checks when they
suspect they are not being paid, which is to say the surface most likely to be read during the grace
window.

**Prevent:** when you add a preview, projection, tooltip or estimate for an existing action, open
the action and copy its guard — do not re-derive one from the same intent. Then write the test in
BOTH directions: the state where the action is skipped must project nothing, and every state where
the action runs must still project (or the fix over-corrects into under-promising).

**Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` findings #10 and #11.

### 100% coverage of a pure function proves nothing about the values that reach it

A pure policy that takes its inputs as parameters can be exhaustively tested — every edge, every
clamp, every null — while the real caller supplies a constant that makes the whole computation
inert. The tests pass literals; the config supplies zero; nobody notices.

**Why missed:** `BattleRenownPolicyTests` covers `BattleRenownPolicy.Compute` in six cases,
including `MeritBandRenownAddsToTheBase` asserting `2 + 5 == 7`. Meanwhile no default band and no
shipped config key ever set `MeritBand.Renown`, so the live value was always 0, every battle paid
the same flat base, and the policy's own doc comment asserting that "the band figure does the
differentiating" was false for the whole life of the feature.

**Prevent:** for every pure policy, add one test on the SUPPLY side — assert the shipped defaults
(and, where it is a shipped asset, the file itself) actually produce values that make the policy do
something. "Does this config key have a non-default value anywhere in the product?" is a different
question from "does the function handle this value?", and only the first one catches dead config.
This is the sibling of the first-pass lesson about pure-policy tests creating false coverage: that
one was about the seam BELOW the policy (ordering, persistence, wiring); this one is about the seam
ABOVE it.

**Source:** `docs/reviews/rca-enlistment-field-fixes-2026-08-11.md` finding #9.

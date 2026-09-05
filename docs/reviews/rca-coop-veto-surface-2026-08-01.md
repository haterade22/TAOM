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

---

# Addendum — the Codex round (same day, three passes)

After the fixes above, three Codex passes ran: the interop changeset, the BannerlordCoop authority
layer (which no review had covered), and one narrow engine-timing question. They returned **3 P1,
5 P2, 1 P3**. Every finding below was re-verified against source before being accepted.

The authority-layer pass — the one aimed at code no reviewer had looked at — produced the most.

## Findings

| # | Sev | Bug | Why missed | Status |
|---|-----|-----|-----------|--------|
| 7 | P1 | `DiplomacyBehavior.OnSessionLaunched` → `EnforcePermanentAlliances` → `MakePeace`/`StartAlliance`, ungated, on every peer including a joining client | **The fifth veto path.** The scan test written *for this exact bug class* could not see it: it hunts consumers of the three predicates, and this path consults none — it mutates from config directly | Fixed; scan extended to direct mutators |
| 8 | P1 | The siege split (finding #3's fix, hours old) let a client claim a reward it never earned | I reasoned that the reward is per-player because it pays `Hero.MainHero`. Its *preconditions* are not: `PlayerAccepted`/`RewardClaimed` live on shared `_activeEvents`, and a joining client's baseline for that save key **is the host's save** | Reverted, then properly fixed in parallel with `_locallyClaimed` per-peer claim state |
| 9 | P1 | `new CareerQuest` on a client is `MBObjectBase` construction in a live campaign | "Per-player, therefore safe" was treated as sufficient. It is not — Coop suppresses the `StringId` setter | **OPEN** — recorded in `coop-interop.md`; needs a live session |
| 10 | P2 | `WarOfTheRingMomentumBehavior.OnSessionLaunched` mutated `RestoreFlags`/`EndWar` *above* its authority gate, driven by an unsynced MCM value | Gate was placed at the interesting call (`SweepEnrollment`) rather than at the top of the handler | Fixed |
| 11 | P2 | `IsAuthority` is Coop-specific and fails **open**, so under BannerlordTogether it reports true on both peers and gates nothing | Two gate concepts (presence vs authority) with no single correct primitive | Fixed in parallel: `ICoopSessionProvider.ShouldDeferToHost` |
| 12 | P2 | Messenger: a client pays `MessengerGoldCost` and enqueues, but processing is authority-only — the messenger never arrives and **the gold is lost** | Only the processing side was audited. Nobody asked whether the *entry point* was reachable | **OPEN** — needs owner identity on `PendingMessenger` |
| 13 | P2 | Siege defence: a client can be prompted and accept, but never reaches the reward tick | Same shape as #12 | Partly addressed; prompt path still needs an owner check |
| 14 | P2 | `CareerQuestCampaignBehavior` dedup scanned `QuestManager.Quests` globally, so the host's active quest blocked the client from ever being offered one | The doc justified leaving this ungated as "keyed entirely on `Hero.MainHero`" — **false when written**, written twice, believed once | Fixed — filters on `CareerQuest.OwnerHeroStringId` |
| 15 | P3 | The veto scan accepts `IsAuthority` as sufficient and missed `EnforcePermanentAlliances` entirely | See #7 and #11 | Fixed |

**Clean, and worth recording as clean:** `CultureConversionBehavior`, `RaceAgeBehavior`,
`WarOfTheRingBehavior`, `CastleRecruitmentBehavior`, `CoopSessionProvider`/`CoopSessionPolicy`, the
assembly-redirect removals, `CoopUiRegistrationPolicy`'s type selection, and
`coop-force-active.flag`'s additive-only semantics.

**One question settled rather than hedged.** Two passes independently decompiled v1.4.7's
`Module.Initialize` and confirmed `ModuleHelper`'s `_loadedModules` is populated **before**
`LoadSubModules` invokes `OnSubModuleLoad`. The UI-registration read is therefore reliable; the
"may not be populated this early" caution was superstition about the pre-managed native string, and
the redundant re-probe it motivated has been removed.

## Second root-cause pattern: the gate is on the exit, the entry is still open

Findings #12, #13 and #9 share a shape that is the **inverse** of the divergence the whole layer was
built to stop. The gate correctly prevents a client mutating shared state — and the entry point in
front of it is still client-reachable. So nobody desyncs; the client simply starts something it can
never finish, and in the Messenger case pays for it.

Every audit so far, mine and the agents', asked *"can a client corrupt shared state?"*. None asked
*"can a client begin a flow the gate later refuses to complete?"* A `RegisterEvents` enumeration
finds the first question and structurally misses the second, because the offending code is a UI
prompt or a spend, not an event handler.

**Rule:** when gating a behaviour host-only, enumerate its *player-facing entry points* — inquiries,
menu options, gold spends, quest offers — and confirm each is either suppressed on a client or
completes locally. A gate on the processing side alone converts a divergence bug into a silent
dead-end.

## Third pattern: a claim written down is not a claim verified

Finding #14's justification ("keyed entirely on `Hero.MainHero`") appeared in a code comment, then in
`coop-interop.md`, then in a review prompt — and was false the whole time. It survived because each
reader treated the previous writing as evidence. The only reason it broke was that the Codex prompt
explicitly said *"verify that claim"* rather than restating it as context.

**Rule:** when a design doc asserts why something is safe, the assertion is a hypothesis with a
citation owed. Carry the *reason* into review prompts as a thing to attack, never as a premise.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->

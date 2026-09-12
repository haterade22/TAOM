# RCA: FiefGranting siege participation (#565), 2026-09-11

Five parallel deep-review agents (standards, API compatibility, efficiency, completeness, data flow)
on the #565 changeset, followed by two `/investigate` runs on the data-flow gaps and a Codex
adversarial pass (appended below when it reports). **Seven findings, two of them real defects in
the new code, two refuted, one pre-existing, two housekeeping.** Both defects were in the one place
the design deliberately copied from vanilla instead of deriving from the engine's own transfer
logic, and both have the same shape as a lesson already on file for this feature.

Also fixed before the review gate, caught by the full suite rather than an agent: the recorder's
clan fallback called `PartyBase.Owner`, a throwing computed getter that `PartyOwnerGetterBanTests`
forbids assembly-wide. The test's own error text names the replacement (`MobileParty.Owner`).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | A `SiegeOutside` (relief battle) attacker victory wrote a participation record although `KingdomManager.SiegeCompleted` (v1.4.8 KingdomManager.cs:238-240) transfers ownership only for `Siege`, `SallyOut` and `BlockadeSallyOutBattle`. Nothing cleared it, and `KingdomManager.RelinquishSettlementOwnership` creates an election with no owner change in front of it, so the phantom record was readable. | Mirrored the wrong sibling | The gate was copied from `SiegeAftermathCampaignBehavior.OnMapEventEnded`, which splits loot and therefore counts every won siege battle. The feature doc even said "same test as vanilla's aftermath handler" as if that settled it. The lesson "Same shape as the sibling is a design statement, not a correctness one" was on file (`state-lifecycle-save.md`) and not consulted for a gate that looked like a mechanical mirror. | Gate now mirrors the transfer set; `FiefGrantingBehaviorCaptureGateTests` pins every `BattleTypes` value; lesson below. |
| 2 | HIGH | Mercenary-service clans were recorded, but `SettlementClaimantDecision.DetermineInitialCandidates` keeps them off the ballot. Every share is measured against the top clan in the record, so a mercenary company that out-fought the field capped every eligible clan below 1.0 and silently denied the full bonus the hint text promises. | Eligibility mirror incomplete | The recorder answered "who fought" and the election answers "who may win"; the two sets were assumed equal. Vanilla's candidate filter had been read (it is quoted in the plan) but never compared against the record's admission rule. | `CanClaimFief` skips `IsUnderMercenaryService`, the only ballot filter knowable at record time; three tests pin it; `FiefSiegeParticipationBindingTests` pins the property. |
| 3 | MED | `FiefGrantingCampaignBehavior` was 176 lines against ADR-002's 150 ceiling (XML documentation, not logic). | Standards | Not measured until an agent measured it. | Trimmed to 148 by moving the argument into the feature doc and keeping pointers. |
| 4 | MED (disputed) | Efficiency: cache the top contribution per record instead of scanning values on every `GetContributionShare` call (3N calls per election). | Efficiency | Real but tiny: a record holds at most a few dozen clans, an election runs once per capture, so the scan is a few hundred comparisons. | Rejected under the simplicity criterion: a second cached value that must stay consistent with the dictionary, for a win nobody will measure. Recorded here so the next reader does not re-derive it. |
| 5 | LOW (refuted) | Efficiency: use the `string.Join(char, ...)` overload in `SnapshotForSave`. | Efficiency | The overload does not exist on .NET Framework 4.7.2; it arrived with .NET Core 2.0. | None. Noted as an agent false positive: an API suggestion is a claim about the target framework. |
| 6 | LOW (pre-existing) | `Patch70_FiefGrantDecisionSwap.cs` is 161 lines against the same ceiling; it was 160 before this change added one comment line. | Standards | Out of scope for #565; the RCA of 2026-08-14 recorded it at 149 with a different counting method. | Recorded, not restructured: edit-scope discipline. Worth its own trim when Patch70 is next touched. |
| 7 | INFO | The feature doc's own producer table listed three producers while a later paragraph reasoned about two ("a siege writes its own record and everything else clears first"), which is how finding 1 hid in a document that was internally inconsistent. | Documentation | The paragraph was written from the capture path forward, exactly the shape the 2026-08-14 RCA's finding #5 warned about for this same feature. | Paragraph rewritten to walk all three producers. |

## Root-cause pattern

Findings 1 and 2 share one shape: **the recorder was specified by analogy to a vanilla handler that
answers a neighbouring question.** The loot handler asks "who was on the winning side of a siege
battle"; ownership transfer asks "did this battle capture the place"; the ballot asks "who may be
granted it". The record sits between the second and the third and must satisfy both, and copying
the first satisfied neither. The correct derivation was available in the same two engine files the
plan already cited (`KingdomManager.SiegeCompleted` and `DetermineInitialCandidates`); it was not
applied to the gate because the gate looked mechanical.

This is the second time this feature has tripped over reasoning from one path instead of
enumerating them. The 2026-08-14 RCA (finding #5) recorded "enumerate producers by grepping the
constructor, not by tracing one path" after the doc claimed two producers where there were three.
Finding 7 is that lesson recurring in the same document: the producer table had three rows and the
prose two paragraphs later reasoned about two of them.

## Why each agent missed what it missed

- **Standards** measured the line counts nobody else did and correctly did not see the two logic
  gaps: its rules are about shape, not about which vanilla set a gate should mirror.
- **API compatibility** verified 35 members and, asked directly, proved the dispatch ordering and
  the single `ApplyBySiege` caller. It confirmed the gate "is an exact behavioral mirror" of the
  loot handler, which was true and was the wrong reference. The question it was asked ("does it
  mirror the aftermath handler") baked the error in; the question that would have caught it is
  "does it mirror the transfer".
- **Efficiency** produced one deferrable and one framework-incorrect suggestion; its scope does not
  include control flow.
- **Completeness** found every doc claim present and matching the code, which they were. It cannot
  see that a matching claim is false about the engine.
- **Data flow** found both defects, by doing what the prompt asked: refute each design claim with a
  reachable scenario. Finding 1 came from walking the third producer; finding 2 from comparing the
  record's admission rule against the ballot's. Both are "compare the two ends of the pipe" checks,
  which is the agent's whole remit.

## Lessons to codify

Appended to `docs/reviews/lessons/campaign-mechanics.md`:

1. **A record that feeds a later decision must mirror the DECISION's admission rule and the ENGINE's
   transfer rule, not a neighbouring handler's.** Name the consumer of the record and the engine
   method that makes the state change real, and derive the write gate from those two, never from
   the handler that merely fires at the same moment.
2. **A persisted stamp answers "who did this last", not "who is doing this now".** `Town.LastCapturedBy`
   is the original #565 defect and the phantom relief-battle record was the same defect reproduced
   in TAOM's own store: anything written on an event and never expired is a memory, and every reader
   must ask whether the event it remembers is the one being decided.

## Verification after fixes

- `dotnet build TAOM.Tests`: clean.
- FiefGrant subset: 95 tests green (record 25, policy 32, behavior session-reset and gates 11,
  capture gate and eligibility 14 including both RED-then-GREEN regressions, binding 4 + 4).
- Ban, fingerprint and fief subsets together: 125 green.
- Full suite: see the CHANGELOG entry for the final count; the one standing red,
  `ShippedCultures_EveryBannerBearerReplacementWeaponIsOneHanded`, is the documented
  `wm_gondor_sword_a04` live-Armory drift and predates this change.
- `python tools/lint_docs.py`: zero long dashes in new prose; the CLAUDE.md row cap met after one
  trim; CLAUDE.md size warning pre-existing.

## Open

- In-game smoke tests (four, listed in the feature doc). Still the only evidence that settles
  whether the campaign plays right.
- Codex adversarial pass: dispatched 2026-09-11 on GPT-6-Astra at ultra; findings appended below.

## Codex adversarial pass (GPT-6-Astra at ultra, appended after the Claude agents)

Codex returned **6 findings: 0 P1, 4 P2, 2 P3**, and eight suspect verdicts: five survived
(ordering, capture gate, no-record parity, MCM rename, save round trip) and three were refuted (keep
and forget, eligibility mirror, share arithmetic at restore). It decompiled the installed MCM loader
to prove the rename reaches existing installs, executed the service source in memory to demonstrate
the overflow, and could not build (an `MSB4184` access-denied on its own machine), so it claimed no
test run. Every finding was verified against the source before anything changed.

| # | Sev | Finding | Verdict | Disposition |
|---|---|---|---|---|
| F1 | P2 | An allied faction's party may join an assault (`SiegeEvent.CanPartyJoinSide`: friendly to every besieger, hostile to every defender) but can never be a candidate (`DetermineInitialCandidates` enumerates the capturing kingdom's clans), so it could set the top share and cap every real candidate. | Confirmed against the engine. Same shape as the mercenary gap, one level wider. | **Fixed**: `FiefSiegeCaptureRules.CanClaimFief` also requires `clan.MapFaction == capturingFaction`, the winning side's leader-party faction, which is what `KingdomManager.SiegeCompleted` hands the settlement to. Test with a foreign clan. |
| F2 | P2 | A capture that opens no claim (a sole-clan kingdom, or any owner that is not a multi-clan kingdom) gets no grant, so nothing ever clears its record; a relinquish or annexation election years later reads it. | Confirmed against `SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged` (`Clans.Count > 1` gate, lines 39-42). | **Fixed**: on `BySiege` the record is kept only when `FiefSiegeCaptureRules.WillOpenAClaim` mirrors that gate; otherwise it is forgotten in the same handler. Five tests on a staged kingdom. |
| F3 | P2 | A duplicated clan id in a hand-edited save string was summed unchecked, wrapping a long into a negative share the policy scored as absent. | Confirmed by reading the restore loop; the writer never emits duplicates. | **Fixed**: a repeated id is malformed and skipped, first occurrence stands. Test with `long.MaxValue` plus a duplicate. |
| F4 | P2 | The feature doc said a pending election "keeps the scoring it was created with"; the weights are read live on every merit calculation, only the type swap is creation-time. | Confirmed; the sentence predates #565 and was carried forward. | **Docs corrected** in the Configuration section and the retune steps. |
| F5 | P3 | `coop-interop.md` still said 213 settings in `TaomSettings` and 57 excluded; the reflected counts are 222 and 59. | Confirmed; both numbers were already wrong before #565 and the doc-count test only checks the headline totals. | **Docs corrected**. |
| F6 | P3 | The Siege Participation Bonus hint said "the attacking side" (a sally-out's winning besiegers are the defender side) and "half the leader's contribution" (the denominator is the top clan, not the leader). | Confirmed. | **Hint rewritten**, and it now states that mercenaries and allied outsiders are not counted. |

Observations acted on without a finding number: the smoke test "your clan does not win" overclaimed
what a multiplier can guarantee (the ballot and a player ruler's choice can still award an absent
clan) and now says damped; the absent factor's effect on `DetermineSupportOption` vote tiers is
documented as the vanilla consumption of merit it is; the inherited co-op limitation (a
`TaomSettlementClaimantDecision` restored from the host's save never passes through
`Kingdom.AddDecision`, so Patch70's `ShouldDeferToHost` skip does not reach it) is recorded in the
feature doc's Co-op section as unverified rather than fixed, since it predates #565 and Codex
itself declined to infer a desync from it.

The fix for F1 and F2 pushed the behavior to 162 lines, so the four engine-derived rules moved into
`FiefSiegeCaptureRules` (internal static, the `FiefGrantFactsBuilder` split), which also let the
regression tests call them directly instead of through reflection. Behavior: 137 lines.

### Root cause of the Codex findings

F1 and F2 are the deep-review pattern a second time: **the recorder was still specified one engine
site short.** After the first fix it mirrored the transfer gate and the mercenary filter; it did not
yet mirror the ballot's kingdom scope (F1) or the condition under which vanilla opens a claim at all
(F2). The complete set the record depends on is now written down in `FiefSiegeCaptureRules`, one
method per engine site, with the site cited in each doc comment. The lesson below is the
generalisation: derive a record's rules from the consumer's admission rule, the engine's transfer
rule, and the engine's "will this decision even exist" rule, and enumerate all three before
mirroring any of them.

F3 is a different shape: the restore path accepted an input the writer never produces and reasoned
about it as if the writer's invariant held. A parser is a boundary; the writer's invariants do not
cross it.

### Why the Claude agents missed the Codex findings

- **Data flow** walked the sole-clan kingdom and the annexation producer for staleness but keyed on
  "was there an owner change" rather than "will vanilla open a claim", and it tested the mercenary
  filter without asking what other winning-side clans the ballot excludes. Both are one enumeration
  wider than the questions it was asked.
- **Compatibility** verified the loot-handler mirror rather than the transfer mirror (same as the
  first pass) and had no question about restore-path input validation.
- **Standards, efficiency, completeness** do not look at engine admission rules or parser inputs.

### Codex process notes

Codex's own build failed on its machine (`MSB4184`, access denied to the Microsoft SDKs folder), so
it ran the service and policy sources in an in-memory harness instead and said so; that is the
honest form of "not run". Its MCM decompile corrected the repo's shorthand: the loader path is
`BaseSettingsJsonConverter.ReadJson` enumerating current property definitions, not a
`PopulateObject`, with the same effect (an orphaned key is ignored, a missing key keeps the compiled
default). `AGENTS.md` no longer carries a "Lessons From Prior Reviews" section, so the
`/review-codex` step that updates it had nothing to update; noted rather than re-created.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

<!-- backlinks-end -->

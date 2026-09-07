# RCA: Patch84 siege aftermath menu guard (#557), deep-review 2026-09-07

**Feature:** `Patch84_SiegeAftermathMenuGuard` plus a siege diagnostic in `EnlistmentBattleBehavior`.
**Crash bundle:** `d7d9f7d3` (TAOM v2.0.27.0, Bannerlord v1.4.7.117484).
**Review:** `/deep-review`, 5 core agents.

## Top line

The shipped guard is correct and the review confirmed it on every axis that matters: patch
registration and apply timing, prefix skip parity against both vanilla bodies, the menu text-variable
contract, the Continue option's safety, sibling `_besiegerParty` reachability, enum polarity,
lifecycle reset, and 26 engine API members verified against the installed DLLs.

**What the review caught was not in the code. It was in the reasoning written around the code.** The
investigation asserted a campaign-event dispatch order it had never verified, used that assertion to
rule out the true root cause, and then wrote the false claim into a code comment, two docs, the
CHANGELOG and a GitHub issue. The guard was right; the story attached to it was wrong, and the story
is what a future session would have trusted.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 1 | HIGH | Claim "vanilla's `MapEventEnded` listener runs first, so TAOM's detach is ruled out" is FALSE. `MbEvent<T>.AddNonSerializedListener` head-inserts and `Invoke` walks from the head, so dispatch is LIFO and TAOM's later-registered listener runs FIRST. TAOM's own `LeaveArmy` is the mechanism that empties `_besiegerParty`. | Unverified engine-semantics assertion used as a negative proof | Dispatch order was inferred from module load order and never decompiled. `MbEvent` was never opened. | New lesson, below. Any ordering claim used to EXCLUDE a suspect must cite decompiled evidence. |
| 2 | LOW | Docs said the army-member menu carries "the identical pair" of unguarded dereferences. It carries three, and the first is `.Army` at :325, ahead of `.CurrentSettlement` at :326. | Imprecise claim about read code | The participant method was read in full; the sibling was skimmed for confirmation of a pattern already believed. | Corrected in all three places. Covered by the same lesson: read the second instance as carefully as the first. |
| 3 | LOW | `EncounterManager.StartSettlementEncounter` was never decompiled, so the claim that `Settlement.CurrentSettlement` names the just-captured settlement on the fallback path is unproven. | Unverified link in a fallback path | Fallback correctness was judged "better than a crash" and not traced further. | Recorded as an open question in the feature doc rather than asserted. Follow-up `/research` if the fallback ever renders a wrong name. |
| 4 | DISPUTED | Standards agent rated "three classes in one file" HIGH, citing a comment in `CoopVetoClassificationTests` as a repo convention. | False positive | The cited line explains why that registry is keyed by type name. It is not a layout rule. | None. `Patch37_CrashReport.cs` (9 targets, 10 classes), `LordConversationsConditionPatches.cs` (4/5) and `GauntletSceneNotification_OpenScene_Guard_Patch.cs` (3/3) are established precedent. |

## Root-cause pattern: a negative proof was accepted on weaker evidence than a positive one

Findings 1 and 2 share a shape. In both, a claim that *confirmed* the working hypothesis was checked
carefully, and a claim that *eliminated* an alternative was accepted on inference.

The investigation decompiled `SiegeAftermathCampaignBehavior`, `MobileParty.SetAttachedToInternal`,
`MapEventSide`, `MapEvent` and `ArmyMembershipAdapter` to build the case for the null field. Every one
of those supported the conclusion being reached. The single claim that *closed off* a competing
explanation, and therefore cancelled a piece of work the user had explicitly asked for, was the only
load-bearing claim never checked against source.

That asymmetry is the bug. An exclusion is a stronger statement than a confirmation, because nothing
downstream re-tests it: once a suspect is ruled out, no later step revisits it. It should therefore
carry a higher evidence bar, and it carried a lower one.

The self-check that would have fired: **"which claim here, if false, would change what I build?"** The
ordering claim was the only one, and it was the only one unverified.

## Why each agent missed or caught it

| Agent | Outcome |
|---|---|
| Standards (haiku) | Missed finding 1, correctly, and out of scope. Its checklist is ADRs, registration and patch mechanics. It produced one false positive (finding 4) by promoting a code comment to a convention without checking for counter-examples. |
| Compatibility (sonnet) | Verified 26 members and both root-cause mechanics (`Parties` == `_battleParties`, `SetAttachedToInternal`), and caught finding 2. It did not catch finding 1 because it was asked to verify the engine members the code *uses*, and `MbEvent` is not one of them. The false claim was about a type the changeset never touches. |
| Efficiency (haiku) | Not in scope. Decompiled the siege-type properties to cost the diagnostic, and cleared it. |
| Completeness (haiku) | Not in scope. Independently reproduced the build and test counts. |
| **Data Flow (sonnet)** | **Caught finding 1.** It was the only agent asked to falsify the narrative rather than verify the code, and the only one that decompiled a type outside the changeset's own API surface to do it. It also cleared the two highest-risk hypotheses (Continue-option deadlock, ungated sibling read sites) with decompiled evidence. |

The generalisable point: the four agents scoped to "verify what the code does" could not have caught
this, because the defect was in a claim about a type the code never calls. Only the agent explicitly
tasked with *disproving the reasoning* had a route to it. That task framing is what earned the finding
and is worth keeping in the prompt.

## Lessons to codify

Appended to `docs/reviews/lessons/harmony-il.md`:

- **Campaign-event listener dispatch is LIFO**, the durable engine fact.

Appended to `docs/reviews/lessons/testing-qa.md`:

- **An exclusion needs stronger evidence than a confirmation**, the review-discipline rule.

No new feedback memory. The general principle is already `evidence-over-claims.md` §C ("never state a
fact you have not read this turn"); what was missing was the specific recognition that a *negative*
claim is the highest-risk instance of it, and that belongs in the lessons record rather than as a new
always-load rule.

## Outcome

- Guard: unchanged, confirmed correct, ships.
- Narrative: corrected in `Patch84_SiegeAftermathMenuGuard.cs`, `EnlistmentBattleBehavior.cs`,
  `docs/features/map-event-guard.md`, `docs/reference/harmony-patch-registry.md`, `CHANGELOG.md`, and
  as a correction comment on issue #557.
- Detach-ordering fix: back on the table, and still owed. It is surgery on the #551 seam and is being
  put to the user rather than taken unilaterally, because the shape of the fix has real trade-offs.

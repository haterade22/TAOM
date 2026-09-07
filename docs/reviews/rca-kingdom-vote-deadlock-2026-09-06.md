# RCA: Patch80_KingdomVoteDeadlock deep review (2026-09-06)

**Feature:** `Patch80_KingdomVoteDeadlock`, issues [#547](https://github.com/haterade22/TAOM/issues/547) and [#550](https://github.com/haterade22/TAOM/issues/550).
**Review:** 6 agents (standards, engine-API compatibility, efficiency, completeness, cross-system data flow, plus a focused Player Switcher pass added mid-review when the user reported the same symptom from that feature).
**Outcome:** 2 HIGH findings, 1 MED, 1 LOW, all confirmed against the installed v1.4.8 decompile before action. Both HIGH fixed in session. One second root cause found and filed separately. Full suite green at 8288 passed, 0 failed, 3 skipped.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | Seam B closes the decision window without `CampaignEvents.KingdomDecisionConcluded.ClearListeners(__instance)`. Vanilla's `ExecuteDone` does it; `DecisionItemBaseVM.OnFinalize` does not. The listener list holds a strong owner reference, so each closed window leaked the item view model, its `DecisionOptionsList` and its `KingdomElection` for the rest of the session. | Lifecycle / substitution | I read `ExecuteDone` closely, but only to answer one question: "why can I not call this?" I found the `GetChosenOutcomeText` NRE, stopped, and wrote my own close path from the two lines I needed. The rest of `ExecuteDone`'s body was never enumerated as a list of responsibilities I was taking over. | New lessons entry (below). IL call-presence test pinning `ClearListeners` in the seam. |
| 2 | HIGH | Seam A's `catch` returned `true` (defer to vanilla), justified in a comment by "seam B backstops it". False for vanilla `RefreshWith`'s `IsSingleClanDecision()` branch, which calls `GetChosenOutcomeText()` on a null `_chosenOutcome` and throws inside the concrete decision's override, and where no `DecisionItemBaseVM` is ever constructed for seam B to see. The error path traded a hang for a crash. | Failure-mode polarity | Same shape as #1 from the other direction. I reasoned about vanilla `RefreshWith`'s `else` branch, which is the one the bug lives in, and never enumerated its `if` branch. The existing lesson "fall through to vanilla on error is only safe when vanilla is a safe default at THAT call site" was read and quoted in the comment, then applied to only one of the two branches at that call site. | Catch now withdraws instead of deferring. Lessons entry generalised to "enumerate every branch". IL test pinning the withdraw call. |
| 3 | MED | The seam B rationale in the patch comment, registry, CHANGELOG and issue body all described a ballot "going stale after its window opened". `KingdomElection.IsCancelled` is written only in `Setup()` and `StartElection()`, both of which run once per item view model, so that cannot happen. | Doc accuracy | The narrative was written from the mechanism I had in my head (the synchronous war-declaration re-entrancy that makes the NEXT ballot stale) and then reused, unexamined, to explain a seam whose trigger is different. Nobody asked "what actually writes this field?" until the compatibility agent did. | Corrected in all four places. Covered by the existing lessons entry on prose evidence; reinforced below. |
| 4 | LOW | Patch comments cited `GauntletKingdomScreen.OnInitialize (:166)` as a call site. Line 166 is in `IGameStateListener.OnActivate`; `OnInitialize` is a separate empty stub on the same class. | Doc accuracy | Read the line, inferred the enclosing method from proximity rather than reading the method header. | Corrected. Same lessons entry as #3. |
| 5 | n/a | A **second, independent root cause** for the same player-visible symptom, via Player Switcher. Taking over a non-leader member of a ruling clan leaves `Clan.PlayerClan.Leader` as the AI king, so `Supporter.IsPlayer => Clan.Leader.IsHumanPlayerCharacter` is permanently false, `IsPlayerSupporter` is false for every decision, and `StartElection()` resolves each one through `ReadyToAiChoose()` inside the view model constructor. None of Patch80's three seams fire, because all gate on `ShouldBeCancelled()` / `IsCancelled` and `ReadyToAiChoose()` never sets `IsCancelled`. | Scope | Not a defect in this changeset. It was invisible because the investigation started from a player report that named one trigger ("one vote follows another"), root-caused that trigger completely, and never asked what *else* could produce an unclosable window. The second trigger surfaced only because a user volunteered a second report mid-review. | Filed as #550 with two candidate fixes. Lessons entry below on symptom-vs-mechanism scoping. |

## Root-cause pattern: substituting for vanilla means inheriting all of its branches and all of its bookkeeping

Findings 1 and 2 are the same mistake pointing in opposite directions, and they are worth reading together.

In both cases I made a decision about vanilla by studying exactly the part of vanilla that motivated the decision, and then generalised it:

- Seam B **does not call** `ExecuteDone`. I studied `ExecuteDone` to learn why I could not call it, found the NRE, and wrote a replacement from the two lines that mattered to me. `ExecuteDone` had a third responsibility, `ClearListeners`, that I never enumerated because it was not the reason I was reading the method.
- Seam A **does call** vanilla on its error path. I studied vanilla `RefreshWith` to learn what it does wrong, found the multi-clan branch that builds the unclosable window, and wrote a catch that hands control back to it. `RefreshWith` had a second branch, single-clan, that throws outright, and I never enumerated it because it was not the branch the bug lived in.

The unifying failure is that **"I read the vanilla method" is not the same as "I enumerated what the vanilla method does"**. Reading is directed by a question; enumeration is not. When TAOM code replaces, skips, or defers to a vanilla method, the correct unit of analysis is the full list of that method's branches and side effects, written down, each one explicitly either replicated or consciously dropped with a reason.

This is the mirror of the existing lesson "When a Prefix returns false, decompile the FULL call chain and replicate every safety gate", which covers the case where we skip vanilla and must carry its gates forward. That lesson's scope was skip-original prefixes. These two findings show the same duty applies when we *substitute* for a vanilla method we chose not to call, and when we *defer* to a vanilla method on an error path. The existing entry did not fire because neither of these is a `return false` prefix.

Finding 3 is a second-order consequence of the same habit: the prose explaining seam B described the mechanism I had been thinking about rather than the one the code keys on, and survived four separate documents because nothing forced a re-derivation from the field's actual write sites.

## Why each agent missed these

- **Agent 1 (Standards):** correctly scoped to ADR compliance, registration and layering. Nothing in its checklist asks what a patch body fails to do relative to the vanilla method it replaces. It passed, correctly, on its own terms.
- **Agent 2 (Compatibility):** caught findings 3 and 4 and independently corrected my Harmony version assumption. It did not catch 1 or 2 because its brief is "does every member the patch references still resolve", which is a question about the members present, not the members absent.
- **Agent 3 (Efficiency):** the listener leak is a memory defect and therefore arguably in scope, but its checklist frames leaks as "collections that grow without pruning" in TAOM-owned state, not "an engine event list we failed to unsubscribe from". It verified the one TAOM-owned collection (`_announced`) correctly.
- **Agent 4 (Completeness):** scoped to artifacts (tests, docs, issue, IoC), and produced one false positive (claimed the CHANGELOG entry was missing when it was present at line 118, below the concurrent session's entries). Nothing in its brief looks at patch-body semantics.
- **Agent 5 (Data Flow):** caught both HIGH findings, which is consistent with the standing observation that this is the highest-value agent. It found them because its brief told it to trace lifecycle completeness and failure-mode polarity across files, which is exactly the enumeration discipline the two bugs violate. It also decompiled `MbEvent<T1,T2,T3>` on its own initiative to confirm the listener list holds strong references rather than assuming.
- **Agent 6 (Player Switcher), added mid-review:** found finding 5. It existed only because the user supplied a second report. No agent brief in the standing five would have found it, because all five were scoped to the changeset and finding 5 is not in the changeset.

## Lessons to codify

Appended to `docs/reviews/lessons/harmony-il.md`:

1. **Substituting for a vanilla method means inheriting every one of its responsibilities, and deferring to one means inheriting every one of its branches.** Enumerate them in writing before doing either.
2. **A symptom is not a mechanism.** When a player report is root-caused, the question "what else could produce this exact symptom" has to be asked explicitly, because the answer is usually invisible from inside the changeset.

Finding 3 needs no new rule: `lessons/harmony-il.md` already carries "Before documenting when a patch runs, grep every call site" with its corollary that the same evidence bar applies to prose as to code. It was not followed. The corrective is to apply the existing rule to *rationale* prose, not just to call-site prose, and that nuance is folded into entry 1.

## Verification

- Both HIGH fixes landed and are pinned by IL call-presence tests (`SeamB_StillClearsTheConcludedListener_OrItLeaksTheItemViewModel`, `SeamA_FaultPathWithdrawsRatherThanDeferringToVanilla`). Call presence proves the calls have not been deleted, not that they run on every path; the tests say so in their bodies.
- Service test updated for the changed catch semantics (`ShouldSuppressBallot_StalenessCheckThrows_SuppressesAndWarns`).
- Full suite: 8288 passed, 0 failed, 3 skipped.
- **In-game verification is still owed for both issues** and is the only thing that can confirm the fix end to end, since Harmony patches are not applied in the MSTest host.

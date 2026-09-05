# Adversarial review: TAOM Enlistment remediation arc (batches 0-10)

You are reviewing a Bannerlord 1.4.7 total-conversion mod (C#, .NET Framework 4.7.2, Harmony).
The feature lets the player serve as a soldier in an NPC lord's party.

## Scope — review ONLY these commits and the files they touch

Run: `git log --oneline 612e72ea..HEAD` and review the enlistment commits:
f9c5db05, 58d64821, 1e2281cf, cfe8424a, a26aecd2, 155e0e8b, c3f444de, 7a52f08f, 49e06fbd
plus uncommitted enlistment changes (`git status`).

**STRICTLY OUT OF SCOPE — do not review, do not comment on:** anything under
`Main/Features/FieldCommission/`, `Main/Features/BattleLoadDiagnostics/`, or any file whose
diff is only diagnostics gating. Those belong to a concurrent session.

## What this arc fixed (context, not a claim to trust)

An enlisted player never joined ANY of their commander's battles. Root cause was
`MapEvent.CanPartyJoinBattle`, a DIPLOMACY test (requires every opposing party at war with the
joiner's MapFaction) used as a mechanical joinability check. Around that, 10 batches added: a
real-time pump, an encounter-ownership policy, a discharge hand-back invariant, settlement
following, an informed leave/desertion choice, dialog agency, a live status board, and two
re-attach edges.

## Known suspects — check these hardest

1. **NaN gates.** `.claude/rules/csharp-architecture.md` documents this bug class shipping FIVE
   times. Every float/double decision gate must be a POSITIVE requirement so NaN fails safe.
   Check `ServiceMaintenanceService.Pump` budgets, `EnlistmentDialogGateService.EvaluateReleaseRequest`,
   any float->int cast. One near-miss was already caught (a `+= dt` outside the guard's braces).
2. **Ordering.** `ServiceAttachmentService.Assess` checks SettlementExitRequired ABOVE the battle
   branch deliberately (a party in two places rewrites a siege assault to SiegeOutside via
   `MapEvent.AddInvolvedPartyInternal`). `DischargeService.Execute` is an 11-step ordered pipeline.
   Verify each stated ordering reason against the DECOMPILED engine, not the comment.
3. **Re-entrancy.** `EnlistmentReconciler` calls `LeaveSettlementAction`, which dispatches
   `OnSettlementLeft`, which `EnlistmentMaintenanceBehavior` now subscribes and routes back to
   `ReconcileNow`. There is a `_reconcileInFlight` guard. Is it sufficient? Any other cycle?
4. **Co-op authority.** Deferred callbacks (inquiry confirmations) run a frame later than the
   gate that authorised them. `EnlistmentWaitMenuPresenter.ConfirmDesertion` re-checks. Are there
   OTHER deferred paths that do not?
5. **State machine.** Can any sequence strand the player: hidden+inactive with no menu, or in a
   settlement with no menu, or latched in EnlistedBattle so future joins are blocked forever?
6. **The MCM kill switch.** `EnlistmentFeatureSettingsProvider` fails open. Flipping it OFF
   mid-service triggers one discharge. Can that fire repeatedly, or race the discharge pipeline?
7. **Engine signatures.** Verify against the INSTALLED DLLs
   (`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/`), never
   the decompile dump. Especially: `CampaignMapConversation.OpenConversation`,
   `ConversationManager.OpenMapConversation`, `GameMenu.RunMenuOptionConsequence` + `isLeave`,
   `EnterSettlementAction`/`LeaveSettlementAction`, `PlayerEncounter.Finish`.

## Architecture rules that are violations if broken

- ADR-002: entry points (CampaignBehaviorBase / Harmony patches / GameModel) < 150 lines and
  delegate to services. Report ANY enlistment entry point over 150 lines.
- ADR-007: services never reference sealed TaleWorlds types (Hero, MobileParty, Settlement) —
  only `IXxxAdapter` interfaces. Report any leak.
- Constructor injection only; no `IoC.Resolve` outside boundary classes.

## Output

Findings ONLY, ranked by severity (P1 = crash/save-corruption/soft-lock, P2 = wrong behaviour,
P3 = smell). For each: file:line, what is wrong, why it is wrong (cite the decompiled engine
source where relevant), and the minimal fix. If you cannot verify a claim, say UNVERIFIED rather
than asserting. Do not restate what the code does correctly. Be adversarial: assume the author
was over-confident.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->

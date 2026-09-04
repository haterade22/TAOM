using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The policy is pure precisely so it can be tested like this. When it is wrong the player either
/// loses their own settlement visit / battle, or is left permanently unable to interact with
/// anything — both were live bugs on 2026-08-07.
/// </summary>
[TestClass]
public class EncounterOwnershipPolicyTests
{
    private readonly EncounterOwnershipPolicy _policy = new EncounterOwnershipPolicy();

    private static EncounterOwnershipSnapshot CommanderEncounter() =>
        new EncounterOwnershipSnapshot(hasEncounter: true, hasEncounteredMobileParty: true,
            encounteredPartyId: "lord_party_1", encounteredPartyIsCommanderRelated: true);

    private static EncounterOwnershipSnapshot SettlementVisit() =>
        new EncounterOwnershipSnapshot(hasEncounter: true, hasEncounteredMobileParty: false,
            playerInsideSettlement: true);

    /// <summary>Settlement-shaped, but the party has already left the settlement: the strand.</summary>
    private static EncounterOwnershipSnapshot StrandedSettlementEncounter() =>
        new EncounterOwnershipSnapshot(hasEncounter: true, hasEncounteredMobileParty: false,
            playerInsideSettlement: false);

    /// <summary>The loot/aftermath window: encounter still open, map event already cleared.</summary>
    private static EncounterOwnershipSnapshot BattleAftermath() =>
        new EncounterOwnershipSnapshot(hasEncounter: true, hasEncounteredMobileParty: false,
            playerInMapEvent: false, isBattleEncounter: true);

    private static EncounterOwnershipSnapshot SomeoneElsesParty() =>
        new EncounterOwnershipSnapshot(hasEncounter: true, hasEncounteredMobileParty: true,
            encounteredPartyId: "some_other_lord", encounteredPartyIsCommanderRelated: false);

    // ---- R0: nothing live -----------------------------------------------------------------

    [DataTestMethod]
    [DataRow(EncounterFinishIntent.OathHandoff)]
    [DataRow(EncounterFinishIntent.StaleBeforeCommanderBattle)]
    [DataRow(EncounterFinishIntent.JoinRollback)]
    [DataRow(EncounterFinishIntent.ParkedSweep)]
    [DataRow(EncounterFinishIntent.Discharge)]
    [DataRow(EncounterFinishIntent.ShoreLeaveEnd)]
    public void Evaluate_NoEncounter_NothingToFinishForEveryIntent(EncounterFinishIntent intent)
    {
        Assert.AreEqual(EncounterFinishVerdict.NothingToFinish,
            _policy.Evaluate(intent, EncounterOwnershipSnapshot.None));
    }

    // ---- R1: never tear down the player's own battle ---------------------------------------

    [DataTestMethod]
    [DataRow(EncounterFinishIntent.OathHandoff)]
    [DataRow(EncounterFinishIntent.StaleBeforeCommanderBattle)]
    [DataRow(EncounterFinishIntent.JoinRollback)]
    [DataRow(EncounterFinishIntent.ParkedSweep)]
    [DataRow(EncounterFinishIntent.Discharge)]
    public void Evaluate_PlayerInTheirOwnMapEvent_DefersForEveryIntent(EncounterFinishIntent intent)
    {
        // Universal and checked before intent — even a discharge must not delete a battle the
        // player is fighting out from under them.
        var snapshot = new EncounterOwnershipSnapshot(hasEncounter: true, hasEncounteredMobileParty: true,
            encounteredPartyId: "lord_party_1", encounteredPartyIsCommanderRelated: true,
            playerInMapEvent: true);

        Assert.AreEqual(EncounterFinishVerdict.DeferPlayerOwnBattle, _policy.Evaluate(intent, snapshot));
    }

    // ---- the critical pin ------------------------------------------------------------------

    [TestMethod]
    public void Evaluate_Oath_SettlementEncounter_SkipsNotOurs()
    {
        // THE pin for this batch. Swear an oath inside a town keep: the encounter is the
        // SETTLEMENT'S, and finishing it ends the player's town visit. A settlement encounter is
        // identified by having no encountered MOBILE party.
        Assert.AreEqual(EncounterFinishVerdict.SkipNotOurs,
            _policy.Evaluate(EncounterFinishIntent.OathHandoff, SettlementVisit()));
    }

    [TestMethod]
    public void Evaluate_Oath_CommanderEncounter_Finishes()
    {
        Assert.AreEqual(EncounterFinishVerdict.Finish,
            _policy.Evaluate(EncounterFinishIntent.OathHandoff, CommanderEncounter()));
    }

    [TestMethod]
    public void Evaluate_Oath_SomeoneElsesParty_SkipsNotOurs()
    {
        Assert.AreEqual(EncounterFinishVerdict.SkipNotOurs,
            _policy.Evaluate(EncounterFinishIntent.OathHandoff, SomeoneElsesParty()));
    }

    // ---- R2: conversations ------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(EncounterFinishIntent.OathHandoff)]
    [DataRow(EncounterFinishIntent.StaleBeforeCommanderBattle)]
    [DataRow(EncounterFinishIntent.JoinRollback)]
    [DataRow(EncounterFinishIntent.ParkedSweep)]
    [DataRow(EncounterFinishIntent.Discharge)]
    public void Evaluate_ConversationInProgress_NeverFinishes(EncounterFinishIntent intent)
    {
        var snapshot = new EncounterOwnershipSnapshot(hasEncounter: true, conversationInProgress: true,
            hasEncounteredMobileParty: true, encounteredPartyId: "lord_party_1",
            encounteredPartyIsCommanderRelated: true);

        Assert.AreEqual(EncounterFinishVerdict.SkipConversationInProgress, _policy.Evaluate(intent, snapshot));
    }

    // ---- discharge outranks ownership -------------------------------------------------------

    [TestMethod]
    public void Evaluate_Discharge_SettlementEncounter_StillFinishes()
    {
        // Service is ending. Leaving ANY encounter live is the save-breaker — EncounterManager
        // refuses every main-party encounter while PlayerEncounter.Current is set.
        Assert.AreEqual(EncounterFinishVerdict.Finish,
            _policy.Evaluate(EncounterFinishIntent.Discharge, SettlementVisit()));
    }

    [TestMethod]
    public void Evaluate_Discharge_SomeoneElsesParty_StillFinishes()
    {
        Assert.AreEqual(EncounterFinishVerdict.Finish,
            _policy.Evaluate(EncounterFinishIntent.Discharge, SomeoneElsesParty()));
    }

    [TestMethod]
    public void Evaluate_Discharge_CommanderUnresolvable_StillFinishes()
    {
        // The commander is dead or gone, so nothing is "commander-related" any more. The discharge
        // must still hand the player back.
        var snapshot = new EncounterOwnershipSnapshot(hasEncounter: true, hasEncounteredMobileParty: true,
            encounteredPartyId: "lord_party_1", encounteredPartyIsCommanderRelated: false);

        Assert.AreEqual(EncounterFinishVerdict.Finish,
            _policy.Evaluate(EncounterFinishIntent.Discharge, snapshot));
    }

    // ---- the non-oath service intents --------------------------------------------------------

    [DataTestMethod]
    [DataRow(EncounterFinishIntent.StaleBeforeCommanderBattle)]
    [DataRow(EncounterFinishIntent.JoinRollback)]
    [DataRow(EncounterFinishIntent.ParkedSweep)]
    public void Evaluate_ServiceIntents_CommanderEncounter_Finish(EncounterFinishIntent intent)
    {
        Assert.AreEqual(EncounterFinishVerdict.Finish, _policy.Evaluate(intent, CommanderEncounter()));
    }

    [DataTestMethod]
    [DataRow(EncounterFinishIntent.StaleBeforeCommanderBattle)]
    [DataRow(EncounterFinishIntent.JoinRollback)]
    [DataRow(EncounterFinishIntent.ParkedSweep)]
    public void Evaluate_ServiceIntents_SettlementVisit_SkipNotOurs(EncounterFinishIntent intent)
    {
        Assert.AreEqual(EncounterFinishVerdict.SkipNotOurs, _policy.Evaluate(intent, SettlementVisit()));
    }

    [DataTestMethod]
    [DataRow(EncounterFinishIntent.StaleBeforeCommanderBattle)]
    [DataRow(EncounterFinishIntent.JoinRollback)]
    [DataRow(EncounterFinishIntent.ParkedSweep)]
    public void Evaluate_ServiceIntents_SomeoneElsesParty_SkipNotOurs(EncounterFinishIntent intent)
    {
        Assert.AreEqual(EncounterFinishVerdict.SkipNotOurs, _policy.Evaluate(intent, SomeoneElsesParty()));
    }

    // ---- R2b: shore leave inverts R3, and only R3 ------------------------------------------
    // The settlement-shaped encounter under ShoreLeaveEnd is the one TakeTownLeave opened so the
    // vanilla town menu would not NRE (issue #510). Ending the pass is the one moment it is ours,
    // and no other mechanism would ever close it — the parked sweep skips it by R3 above.

    [TestMethod]
    public void Evaluate_ShoreLeaveEnd_SettlementEncounter_Finish()
    {
        Assert.AreEqual(EncounterFinishVerdict.Finish,
            _policy.Evaluate(EncounterFinishIntent.ShoreLeaveEnd, SettlementVisit()));
    }

    [TestMethod]
    public void Evaluate_ShoreLeaveEnd_CommanderEncounter_Finish()
    {
        // A commander encounter is ours under every service intent; shore leave changes nothing.
        Assert.AreEqual(EncounterFinishVerdict.Finish,
            _policy.Evaluate(EncounterFinishIntent.ShoreLeaveEnd, CommanderEncounter()));
    }

    [TestMethod]
    public void Evaluate_ShoreLeaveEnd_SomeoneElsesParty_SkipNotOurs()
    {
        Assert.AreEqual(EncounterFinishVerdict.SkipNotOurs,
            _policy.Evaluate(EncounterFinishIntent.ShoreLeaveEnd, SomeoneElsesParty()));
    }

    // ---- R2c: a stranded settlement encounter inverts R3 from the other direction -----------
    // `LeaveSettlementAction.ApplyForParty` (installed v1.4.8) finishes the PlayerEncounter only
    // when the leaving party leads its army and the main party is attached to it. An enlisted
    // player is the main party and leads nothing, so a settlement exit can leave the encounter
    // behind. R3 protects a town visit the player owns; out of the settlement there is none, and
    // the leak blocks map movement, every future encounter, and the battle-latch break.
    //
    // The caller owes the precondition (player has no settlement); the reconciler enforces it at
    // the single call site by choosing ParkedSweep instead when the player IS inside one.

    [TestMethod]
    public void Evaluate_StrandedOutsideSettlement_SettlementShapedButPlayerIsOut_Finish()
    {
        // The real stranded shape: settlement-shaped encounter, player NOT in a settlement.
        Assert.AreEqual(EncounterFinishVerdict.Finish,
            _policy.Evaluate(EncounterFinishIntent.StrandedOutsideSettlement, StrandedSettlementEncounter()));
    }

    [TestMethod]
    public void Evaluate_StrandedOutsideSettlement_PlayerActuallyInsideSettlement_SkipsNotOurs()
    {
        // THE PRECONDITION, now enforced by the policy instead of trusted from the caller. The
        // first version of this rule took the caller's word for it and this very test asserted
        // Finish for SettlementVisit(), which is built with playerInsideSettlement:true — so the
        // committed test encoded as correct the exact state R3 exists to protect. The snapshot has
        // carried a freshly-read PlayerInsideSettlement all along; the policy simply never read it.
        Assert.AreEqual(EncounterFinishVerdict.SkipNotOurs,
            _policy.Evaluate(EncounterFinishIntent.StrandedOutsideSettlement, SettlementVisit()));
    }

    [TestMethod]
    public void Evaluate_EveryIntent_BattleEncounter_DefersToTheBattle()
    {
        // R1b. An open encounter with no map event is NOT proof the battle is over: the loot and
        // aftermath menus run inside the still-open encounter, and MapEventSide.Clear() nulls
        // MainParty.MapEvent BEFORE the encounter closes. EnlistmentReconciler's own
        // noBattleAnywhere predicate and ServiceBattleService.OnCommanderBattleEnded both already
        // treat an open encounter as "battle still live" for exactly this reason. Finishing here
        // tears down the player's own loot screen, and PlayerEncounter.Finish also forces
        // TimeControlMode.Stop and GameMenu.ExitToLast().
        foreach (EncounterFinishIntent intent in System.Enum.GetValues(typeof(EncounterFinishIntent)))
        {
            Assert.AreEqual(EncounterFinishVerdict.DeferPlayerOwnBattle,
                _policy.Evaluate(intent, BattleAftermath()),
                $"intent {intent} tore down a battle encounter");
        }
    }

    [TestMethod]
    public void Evaluate_EveryIntent_NothingLive_IsNothingToFinish()
    {
        // Enum-driven so a NEW intent cannot silently miss the universal rules the way
        // StrandedOutsideSettlement missed R0 when it was hand-listed into the DataRow sets.
        foreach (EncounterFinishIntent intent in System.Enum.GetValues(typeof(EncounterFinishIntent)))
        {
            Assert.AreEqual(EncounterFinishVerdict.NothingToFinish,
                _policy.Evaluate(intent, new EncounterOwnershipSnapshot(hasEncounter: false)),
                $"intent {intent} acted on a dead encounter");
        }
    }

    [TestMethod]
    public void Evaluate_EveryIntent_PlayerInOwnMapEvent_Defers()
    {
        foreach (EncounterFinishIntent intent in System.Enum.GetValues(typeof(EncounterFinishIntent)))
        {
            Assert.AreEqual(EncounterFinishVerdict.DeferPlayerOwnBattle,
                _policy.Evaluate(intent, new EncounterOwnershipSnapshot(hasEncounter: true, playerInMapEvent: true)),
                $"intent {intent} tore down the player's own battle");
        }
    }

    [TestMethod]
    public void Evaluate_StrandedOutsideSettlement_CommanderEncounter_Finish()
    {
        Assert.AreEqual(EncounterFinishVerdict.Finish,
            _policy.Evaluate(EncounterFinishIntent.StrandedOutsideSettlement, CommanderEncounter()));
    }

    [TestMethod]
    public void Evaluate_StrandedOutsideSettlement_SomeoneElsesParty_SkipNotOurs()
    {
        // R4 is untouched: widening R3 must not hand us a stranger's party encounter.
        Assert.AreEqual(EncounterFinishVerdict.SkipNotOurs,
            _policy.Evaluate(EncounterFinishIntent.StrandedOutsideSettlement, SomeoneElsesParty()));
    }

    [TestMethod]
    public void Evaluate_StrandedOutsideSettlement_PlayerInOwnBattle_Defers()
    {
        // R1 outranks every intent. A player fighting their own battle keeps it.
        Assert.AreEqual(EncounterFinishVerdict.DeferPlayerOwnBattle,
            _policy.Evaluate(EncounterFinishIntent.StrandedOutsideSettlement,
                new EncounterOwnershipSnapshot(hasEncounter: true, playerInMapEvent: true)));
    }

    [TestMethod]
    public void Evaluate_StrandedOutsideSettlement_ConversationInProgress_Skips()
    {
        // R2 outranks it too: never yank the player out of their own dialogue.
        Assert.AreEqual(EncounterFinishVerdict.SkipConversationInProgress,
            _policy.Evaluate(EncounterFinishIntent.StrandedOutsideSettlement,
                new EncounterOwnershipSnapshot(hasEncounter: true, conversationInProgress: true)));
    }

    [TestMethod]
    public void Evaluate_ParkedSweep_SettlementEncounter_StillSkips()
    {
        // The regression guard for the fix itself. ParkedSweep must NOT gain the new behaviour:
        // it is what the reconciler passes while the player is inside a settlement, and finishing
        // there would take down the live town menu #510 exists to keep safe.
        Assert.AreEqual(EncounterFinishVerdict.SkipNotOurs,
            _policy.Evaluate(EncounterFinishIntent.ParkedSweep, SettlementVisit()));
    }

    [TestMethod]
    public void Evaluate_ShoreLeaveEnd_PlayerInOwnBattle_Defers()
    {
        // Leave is revoked when a battle starts, so this is the common case, not an edge one.
        // Finishing here freezes the map event the player is standing in.
        var inBattle = new EncounterOwnershipSnapshot(hasEncounter: true,
            hasEncounteredMobileParty: true, encounteredPartyId: "lord_party_1",
            encounteredPartyIsCommanderRelated: true, playerInMapEvent: true);

        Assert.AreEqual(EncounterFinishVerdict.DeferPlayerOwnBattle,
            _policy.Evaluate(EncounterFinishIntent.ShoreLeaveEnd, inBattle));
    }

    [TestMethod]
    public void Evaluate_ShoreLeaveEnd_ConversationRunning_Skips()
    {
        var talking = new EncounterOwnershipSnapshot(hasEncounter: true,
            hasEncounteredMobileParty: false, conversationInProgress: true,
            playerInsideSettlement: true);

        Assert.AreEqual(EncounterFinishVerdict.SkipConversationInProgress,
            _policy.Evaluate(EncounterFinishIntent.ShoreLeaveEnd, talking));
    }
}

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
}

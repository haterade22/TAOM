using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NavalTravel;

namespace TAOM.Tests.Features.NavalTravel;

[TestClass]
public class AtSeaRepairTests
{
    private static AtSeaRepairAction Decide(bool atSea = true, bool attached = false, bool naval = false,
        bool transition = false, bool mapEvent = false, bool siege = false, bool inSettlement = false) =>
        AtSeaRepair.Decide(atSea, attached, naval, transition, mapEvent, siege, inSettlement);

    [TestMethod]
    public void PartyOnLand_IsLeftAlone() =>
        Assert.AreEqual(AtSeaRepairAction.None, Decide(atSea: false));

    [TestMethod]
    public void AttachedParty_MovesWithItsArmyLeader() =>
        Assert.AreEqual(AtSeaRepairAction.None, Decide(attached: true));

    [TestMethod]
    public void PartyThatCanSail_IsNotStranded() =>
        Assert.AreEqual(AtSeaRepairAction.None, Decide(naval: true));

    [TestMethod]
    public void PartyMidTransition_IsLeftToVanilla() =>
        Assert.AreEqual(AtSeaRepairAction.None, Decide(transition: true));

    [TestMethod]
    public void PartyInAMapEvent_IsEngineOwned() =>
        Assert.AreEqual(AtSeaRepairAction.SkipEngineOwned, Decide(mapEvent: true));

    [TestMethod]
    public void PartyInASiege_IsEngineOwned() =>
        Assert.AreEqual(AtSeaRepairAction.SkipEngineOwned, Decide(siege: true));

    [TestMethod]
    public void PartyInASettlement_GoesToThatGate() =>
        Assert.AreEqual(AtSeaRepairAction.MoveToOwnSettlementGate, Decide(inSettlement: true));

    [TestMethod]
    public void FreePartyAtSea_GoesToTheNearestGate() =>
        Assert.AreEqual(AtSeaRepairAction.MoveToNearestGate, Decide());

    [TestMethod]
    public void EngineOwned_WinsOverSettlement() =>
        Assert.AreEqual(AtSeaRepairAction.SkipEngineOwned, Decide(mapEvent: true, inSettlement: true));

    [TestMethod]
    public void Module_RegistersTheRepairBehaviorOnly()
    {
        var module = new NavalTravelModule();
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(AtSeaRepairBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.AreEqual(0, module.PatchCategories.Count, "the naval model and patches stay parked");
    }
}

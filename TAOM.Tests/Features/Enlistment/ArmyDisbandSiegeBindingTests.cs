using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// An enlisted player's commander was pulled out of his own siege after a battle. Disbanding the
/// army TAOM raised for the battle runs Army.FinishArmyObjective on the leader, which sets him to
/// Hold, and the next SiegeEvent tick removes every besieger that is not besieging, escorting or
/// assaulting (BesiegerCamp.CheckBesiegerPartiesAndMakeThemLeave). The adapter puts him back on his
/// siege right after the disband. These pins fail if the engine or the adapter changes that shape.
/// </summary>
[TestClass]
[TestCategory("BindingVerification")]
public class ArmyDisbandSiegeBindingTests
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [TestMethod]
    public void FinishArmyObjective_StillSetsTheLeaderToHold()
    {
        var method = typeof(Army).GetMethod("FinishArmyObjective", All);
        Assert.IsNotNull(method, "Army.FinishArmyObjective");

        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray()).ToList();

        Assert.IsTrue(calls.Any(m => m.Name == "SetMoveModeHold" && m.DeclaringType == typeof(MobileParty)),
            "FinishArmyObjective no longer calls SetMoveModeHold: re-check whether the siege restore is still needed.");
    }

    [TestMethod]
    public void BesiegerCamp_StillDropsPartiesThatStoppedBesieging()
    {
        var method = typeof(BesiegerCamp).GetMethod("CheckBesiegerPartiesAndMakeThemLeave", All);
        Assert.IsNotNull(method, "BesiegerCamp.CheckBesiegerPartiesAndMakeThemLeave");

        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray()).ToList();
        Assert.IsTrue(calls.Any(m => m.Name == "get_DefaultBehavior" && m.DeclaringType == typeof(MobileParty)),
            "the drop still reads the party's DefaultBehavior");
        Assert.IsTrue(calls.Any(m => m.Name == "set_BesiegerCamp" && m.DeclaringType == typeof(MobileParty)),
            "the drop still clears the party's BesiegerCamp");
    }

    [TestMethod]
    public void SetMoveBesiegeSettlement_StillTakesSettlementAndNavigation()
    {
        var method = typeof(MobileParty).GetMethod("SetMoveBesiegeSettlement",
            new[] { typeof(Settlement), typeof(MobileParty.NavigationType) });

        Assert.IsNotNull(method, "MobileParty.SetMoveBesiegeSettlement(Settlement, NavigationType)");
        Assert.IsTrue(method.IsPublic);
    }

    // Pins the call ORDER in the IL of DisbandCreatedArmy's straight-line body, not control flow.
    [TestMethod]
    public void DisbandCreatedArmy_PutsTheLeaderBackOnHisSiegeAfterTheDisband()
    {
        var disband = typeof(ArmyMembershipAdapter).GetMethod("DisbandCreatedArmy", All);
        var restore = typeof(ArmyMembershipAdapter).GetMethod("KeepLeaderOnHisSiege", All);
        Assert.IsNotNull(disband, "ArmyMembershipAdapter.DisbandCreatedArmy");
        Assert.IsNotNull(restore, "ArmyMembershipAdapter.KeepLeaderOnHisSiege");

        var calls = IlCallScanner.ExtractCalledMethods(disband, disband.GetMethodBody().GetILAsByteArray()).ToList();
        int apply = calls.FindIndex(m => m.Name == "ApplyByObjectiveFinished" && m.DeclaringType == typeof(DisbandArmyAction));
        int keep = calls.FindIndex(m => m.Name == "KeepLeaderOnHisSiege");
        Assert.IsTrue(apply >= 0, "ApplyByObjectiveFinished is called");
        Assert.IsTrue(keep > apply, $"KeepLeaderOnHisSiege (call #{keep}) comes after ApplyByObjectiveFinished (call #{apply})");

        var inner = IlCallScanner.ExtractCalledMethods(restore, restore.GetMethodBody().GetILAsByteArray()).ToList();
        Assert.IsTrue(inner.Any(m => m.Name == "SetMoveBesiegeSettlement" && m.DeclaringType == typeof(MobileParty)),
            "KeepLeaderOnHisSiege calls MobileParty.SetMoveBesiegeSettlement");
    }
}

using System.Text.RegularExpressions;
using TAOM.Tests.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Messengers;

namespace TAOM.Tests.Features.Messengers;

/// <summary>
/// #898: the messenger conversation enters the target's settlement through <c>PlayerEncounter.EnterSettlement</c>, and
/// <c>EnterSettlementAction.ApplyForParty</c> then attaches a main party that sits in an army it does not lead whose leader
/// is in that settlement. <c>PlayerEncounter.Finish</c> skips <c>LeaveSettlement</c> for an attached party, so the player
/// would stay in the town. That state uses the field conversation path instead.
/// </summary>
[TestClass]
public class MessengerEntryPolicyTests
{
    [DataTestMethod]
    [DataRow(true, false, true, false, true)]
    [DataRow(false, false, true, false, false)]
    [DataRow(true, true, true, false, false)]
    [DataRow(true, false, false, false, false)]
    [DataRow(true, false, true, true, false)]
    public void WouldAttachToArmy_IsTrueOnlyForTheExactStateTheEngineAttaches(
        bool hasArmy, bool leadsArmy, bool leaderInTargetSettlement, bool alreadyAttached, bool expected)
        => Assert.AreEqual(expected, MessengerEntryPolicy.WouldAttachToArmy(hasArmy, leadsArmy, leaderInTargetSettlement, alreadyAttached));

    [TestMethod]
    public void TheSettlementBranch_ChecksThePolicyBeforeEnteringTheSettlement()
    {
        var src = RepoPaths.ReadSource("Main/Features/Messengers/MessengerCampaignBehavior.cs", stripComments: true);

        StringAssert.Matches(src, new Regex(@"if\s*\(\s*currentSettlement\s*!=\s*null\s*&&\s*!WouldAttachToArmy\(\s*currentSettlement\s*\)\s*\)"));
    }
}

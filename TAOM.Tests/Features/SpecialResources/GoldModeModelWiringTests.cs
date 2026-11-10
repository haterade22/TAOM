using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #1109 source pins for the two GameModels. The gold surcharge is gated by the engine argument that
/// identifies the player in each method, and a wrong argument charges (or skips) the wrong party, so
/// each pin names the exact expression. Neither model can be built without a running game.
/// </summary>
[TestClass]
public class GoldModeModelWiringTests
{
    private const string WageModel = "Main/Features/TroopProgression/Models/TaomPartyWageModel.cs";
    private const string UpgradeModel = "Main/Features/CulturalFeats/Models/TaomPartyTroopUpgradeModel.cs";

    private static string Body(string path, string signature) =>
        SpecialResourcesGoldModeWiringTests.MethodBody(path, signature);

    [TestMethod]
    public void GetGoldCostForUpgrade_GatesOnPartyBaseMainParty()
    {
        var body = Body(UpgradeModel, "public override ExplainedNumber GetGoldCostForUpgrade(");

        StringAssert.Contains(body,
            "_specialResources.ApplyGoldUpgradeSurcharge(party == PartyBase.MainParty, upgradeTarget?.StringId, ref result);");
        Assert.IsTrue(body.IndexOf("ApplyGoldUpgradeSurcharge", StringComparison.Ordinal) < body.IndexOf("return result;", StringComparison.Ordinal),
            "the surcharge must be on the number that is returned");
    }

    [TestMethod]
    public void GetTotalWage_SurchargeReadsTroopRosterArgument()
    {
        var body = Body(WageModel, "public override ExplainedNumber GetTotalWage(");

        StringAssert.Contains(body,
            "_specialResources.ApplyGoldUpkeepSurcharge(mobileParty.IsMainParty, PartyUpkeepReader.Enumerate(troopRoster), ref result);");
        // Wage-limit desertion calls GetTotalWage with a roster that is not the party's own.
        Assert.IsFalse(body.Contains("MemberRoster"), "the surcharge must price the roster it was handed, not the party's member roster");
        Assert.IsTrue(body.IndexOf("ApplyGoldUpkeepSurcharge", StringComparison.Ordinal) < body.IndexOf("return result;", StringComparison.Ordinal),
            "the surcharge must be on the number that is returned");
    }

    [TestMethod]
    public void GetCharacterWage_HasNoGoldSurcharge()
    {
        var body = Body(WageModel, "public override int GetCharacterWage(");

        Assert.IsFalse(body.Contains("_specialResources"),
            "Campaign.AverageWage divides by GetCharacterWage: a surcharge here would inflate the garrison-donation math");
    }

    [TestMethod]
    public void GetTroopRecruitmentCost_GatesOnHeroMainHero()
    {
        var body = Body(WageModel, "public override ExplainedNumber GetTroopRecruitmentCost(");

        Assert.IsTrue(Regex.IsMatch(body,
                @"_specialResources\.GetGoldRecruitSurcharge\(buyerHero != null && buyerHero == Hero\.MainHero, troop\.StringId\)"),
            "the only main-player signal this method has is the buyer hero");
    }

    [TestMethod]
    public void BothModels_TakeTheServiceByConstructor()
    {
        foreach (var path in new[] { WageModel, UpgradeModel })
        {
            var text = TAOM.Tests.Infrastructure.RepoPaths.ReadSource(path, stripComments: true);
            StringAssert.Contains(text, "ISpecialResourceService specialResources");
            Assert.IsFalse(text.Contains("IoC.Resolve"), "a GameModel resolves nothing in its hot path: " + path);
        }
    }
}

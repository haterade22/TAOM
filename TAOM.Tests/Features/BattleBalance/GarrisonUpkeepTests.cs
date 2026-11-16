using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleBalance;

namespace TAOM.Tests.Features.BattleBalance;

/// <summary>
/// #1283: a realm whose culture is in SiegeDefence.UpkeepFreeGarrisons pays no garrison wages, its garrisons eat no food
/// and do not starve, and its fortifications gain DailyGarrisonGrowth free troops a day. The decisions are pure here; the
/// wiring tests below pin where the four models call them.
/// </summary>
[TestClass]
public class GarrisonUpkeepTests
{
    private static BattleBalanceConfig.SiegeDefenceSection Section() => new BattleBalanceConfig().SiegeDefence;

    [DataTestMethod]
    [DataRow("bluecraig", true)]
    [DataRow("gondor", false)]
    [DataRow(null, false)]
    public void IsUpkeepFree_OnlyForAListedRealmCulture(string culture, bool expected)
    {
        Assert.AreEqual(expected, Section().IsUpkeepFree(culture));
    }

    [DataTestMethod]
    [DataRow("bluecraig", 2f)]
    [DataRow("gondor", 0f)]
    [DataRow(null, 0f)]
    public void GetDailyGarrisonGrowth_OnlyForARealmCultureWithARow(string culture, float expected)
    {
        Assert.AreEqual(expected, Section().GetDailyGarrisonGrowth(culture), 0.0001f);
    }

    [DataTestMethod]
    [DataRow(true, 100, 20, 5f)]
    [DataRow(false, 100, 20, 0f)]
    [DataRow(true, 0, 20, 0f)]
    [DataRow(true, 100, 0, 0f)]
    public void GarrisonFoodRefund_GivesBackWhatTheGarrisonAte(bool upkeepFree, int members, int menPerFood, float expected)
    {
        Assert.AreEqual(expected, GarrisonUpkeep.GarrisonFoodRefund(upkeepFree, members, menPerFood), 0.0001f);
    }

    [DataTestMethod]
    [DataRow(true, true, -12f, 12f)]
    [DataRow(true, true, 6f, 0f)]
    [DataRow(false, true, -12f, 0f)]
    [DataRow(true, false, -12f, 0f)]
    public void StarvationRefund_CancelsOnlyAFreeGarrisonsStarvationLoss(bool upkeepFree, bool isGarrison, float healing, float expected)
    {
        Assert.AreEqual(expected, GarrisonUpkeep.StarvationRefund(upkeepFree, isGarrison, healing), 0.0001f);
    }

    [TestMethod]
    public void IsUpkeepFree_FalseRow_TurnsTheRealmOff()
    {
        var section = Section();
        section.UpkeepFreeGarrisons["bluecraig"] = false;

        Assert.IsFalse(section.IsUpkeepFree("bluecraig"));
    }

    [TestMethod]
    public void NoSection_IsNeverFree()
    {
        Assert.IsFalse(GarrisonUpkeep.IsUpkeepFree(null, "bluecraig"));
        Assert.AreEqual(0f, GarrisonUpkeep.DailyGrowth(null, "bluecraig"), 0.0001f);
    }

    // Wiring only: the four models read the realm's culture (Settlement.MapFaction, as the siege defence bonus does) and
    // call the helpers above.
    [TestMethod]
    public void Wiring_TheFourModelsApplyTheUpkeepRules()
    {
        var subModule = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var wage = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/TroopProgression/Models/TaomPartyWageModel.cs", stripComments: true);
        var food = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs", stripComments: true);
        var healing = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs", stripComments: true);
        var garrison = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/BattleBalance/Models/TaomSettlementGarrisonModel.cs", stripComments: true);

        var hook = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/BattleBalance/Hooks/FreeGarrisonUpkeepLine.cs", stripComments: true);
        StringAssert.Contains(subModule, "new TaomSettlementFoodModel(IoC.Resolve<ISettlementFoodService>(), IoC.Resolve<ISettlementFoodConfigProvider>(), IoC.Resolve<IBattleBalanceConfigProvider>())");
        StringAssert.Contains(subModule, "new TaomSettlementGarrisonModel(battleBalanceConfig)");

        StringAssert.Contains(wage, "return TAOM.Features.BattleBalance.Hooks.FreeGarrisonUpkeepLine.Wage(mobileParty, result, CultureText);");
        StringAssert.Contains(hook, "GarrisonUpkeep.IsUpkeepFree(Config?.GetConfig()?.SiegeDefence, party.CurrentSettlement?.MapFaction?.Culture?.StringId)");
        StringAssert.Contains(hook, "wage.LimitMax(0f, label);");
        StringAssert.Contains(hook, "GarrisonUpkeep.StarvationRefund(");
        StringAssert.Contains(food, "GarrisonUpkeep.GarrisonFoodRefund(");
        StringAssert.Contains(food, "town.MapFaction?.Culture?.StringId");
        StringAssert.Contains(healing, "FreeGarrisonUpkeepLine.Healing(_configProvider, party, base.GetDailyHealingForRegulars(party, isPrisoners, includeDescriptions))");
        StringAssert.Contains(garrison, "base.CalculateBaseGarrisonChange(settlement, includeDescriptions)");
        StringAssert.Contains(garrison, "GarrisonUpkeep.DailyGrowth(_configProvider?.GetConfig()?.SiegeDefence, settlement?.MapFaction?.Culture?.StringId)");
    }
}

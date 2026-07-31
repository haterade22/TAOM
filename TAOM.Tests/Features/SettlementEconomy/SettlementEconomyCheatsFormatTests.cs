using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SettlementEconomy.Cheats;

namespace TAOM.Tests.Features.SettlementEconomy;

/// <summary>
/// Pins the `taom.print_town_economy` console report.
///
/// Why the command exists: #317 is "towns run out of gold and never recover", and confirming the fix
/// today means loading a save and watching a broke town's market gold for 4–8 in-game days — then
/// toggling MCM off and watching again to tell TAOM's math from vanilla's. The report prints both
/// sides at once. The vanilla column is free rather than duplicated: passing a null config to
/// <c>ComputeTownGoldChange</c> computes with the vanilla constants, so the report can never drift
/// from the engine's own numbers.
/// </summary>
[TestClass]
public class SettlementEconomyCheatsFormatTests
{
    [TestMethod]
    public void FormatTownEconomy_BelowTarget_RendersBothTaomAndVanillaSideBySide()
    {
        // Arrange / Act
        var report = SettlementEconomyCheats.FormatTownEconomy(
            townId: "town_G3", townName: "Minas Tirith", gold: 8412, prosperity: 3180f,
            taomChange: 13687, vanillaChange: 9937, taomTarget: 63160f, rate: 0.25f, enabled: true);

        // Assert
        StringAssert.Contains(report, "town_G3");
        StringAssert.Contains(report, "Minas Tirith");
        StringAssert.Contains(report, "13,687");
        StringAssert.Contains(report, "9,937");
    }

    /// <summary>
    /// The toggle-off case is half the #317 investigation — "is this TAOM's model or vanilla's?" must
    /// be answerable from the report alone.
    /// </summary>
    [TestMethod]
    public void FormatTownEconomy_TuningDisabled_SaysVanillaIsInEffect()
    {
        var report = SettlementEconomyCheats.FormatTownEconomy(
            townId: "town_G3", townName: "Minas Tirith", gold: 8412, prosperity: 3180f,
            taomChange: 13687, vanillaChange: 9937, taomTarget: 63160f, rate: 0.25f, enabled: false);

        StringAssert.Contains(report, "vanilla");
        Assert.IsFalse(report.Contains("in effect: TAOM"));
    }

    /// <summary>
    /// The config's rate is also the mean-reversion rate when a town sits ABOVE its target, so the
    /// change goes negative. A report that assumed recovery would render a nonsense "days to target".
    /// </summary>
    [TestMethod]
    public void FormatTownEconomy_GoldAboveTarget_RendersNegativeChangeAndNoDaysEstimate()
    {
        var report = SettlementEconomyCheats.FormatTownEconomy(
            townId: "town_G3", townName: "Minas Tirith", gold: 90000, prosperity: 3180f,
            taomChange: -6710, vanillaChange: -10515, taomTarget: 63160f, rate: 0.25f, enabled: true);

        StringAssert.Contains(report, "-6,710");
        StringAssert.Contains(report, "above target");
    }

    /// <summary>
    /// `TownGoldRegenRate` is validated on `[0,1]`, so 0 is an explicitly allowed extreme. A
    /// days-to-target estimate must not divide by it.
    /// </summary>
    [TestMethod]
    public void FormatTownEconomy_ZeroRegenRate_SaysFrozenRatherThanDividingByZero()
    {
        var report = SettlementEconomyCheats.FormatTownEconomy(
            townId: "town_G3", townName: "Minas Tirith", gold: 8412, prosperity: 3180f,
            taomChange: 0, vanillaChange: 9937, taomTarget: 63160f, rate: 0f, enabled: true);

        StringAssert.Contains(report, "frozen");
    }

    [TestMethod]
    public void FormatTownEconomy_BelowTargetWithPositiveChange_EstimatesDaysToTarget()
    {
        var report = SettlementEconomyCheats.FormatTownEconomy(
            townId: "town_G3", townName: "Minas Tirith", gold: 8412, prosperity: 3180f,
            taomChange: 13687, vanillaChange: 9937, taomTarget: 63160f, rate: 0.25f, enabled: true);

        StringAssert.Contains(report, "days");
    }

    [TestMethod]
    public void FormatTownEconomy_RendersGoldProsperityAndTarget()
    {
        var report = SettlementEconomyCheats.FormatTownEconomy(
            townId: "town_R1", townName: "Edoras", gold: 500, prosperity: 1200f,
            taomChange: 9800, vanillaChange: 5900, taomTarget: 39400f, rate: 0.25f, enabled: true);

        StringAssert.Contains(report, "500");
        StringAssert.Contains(report, "1,200");
        StringAssert.Contains(report, "39,400");
    }
}

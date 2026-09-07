using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.AiPartySize;

/// <summary>
/// Issue #461. The AI party-size scaling MUST be applied before the TroopWeight elite tax, because
/// ApplyPartySizeWeightPenalty snapshots (int)limit.ResultNumber and caches it as the party's "true
/// base" — the budget the daily shed later trims a heavy party back to. Applied after, the shed
/// keeps trimming to the UNSCALED limit and the entire feature silently does nothing while every
/// unit test still passes.
///
/// That failure is invisible to a normal unit test: both orderings produce the same ExplainedNumber,
/// and the divergence only appears a tick later inside a hook that takes a sealed PartyBase. A
/// source-order assertion is the cheap guard, matching the existing BannerTripletOrderingTests
/// pattern (sealed engine types, ordering verified against the source rather than at runtime).
/// </summary>
[TestClass]
public class AiPartySizeOrderingTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string ReadPartySizeModel()
    {
        var path = Path.Combine(
            FindRepoRoot(), "Main", "Features", "CulturalFeats", "Models", "TaomPartySizeModel.cs");
        Assert.IsTrue(File.Exists(path), $"TaomPartySizeModel.cs not found at {path}");
        return File.ReadAllText(path);
    }

    [TestMethod]
    public void PartySizeModel_AppliesAiScalingBeforeTheTroopWeightEliteTax()
    {
        var src = ReadPartySizeModel();

        int aiScaling = src.IndexOf("ApplyAiLordScaling", System.StringComparison.Ordinal);
        int weightPenalty = src.IndexOf("ApplyPartySizeWeightPenalty", System.StringComparison.Ordinal);

        Assert.AreNotEqual(-1, aiScaling, "TaomPartySizeModel must call ApplyAiLordScaling");
        Assert.AreNotEqual(-1, weightPenalty, "TaomPartySizeModel must call ApplyPartySizeWeightPenalty");
        Assert.IsTrue(
            aiScaling < weightPenalty,
            "ApplyAiLordScaling must run BEFORE ApplyPartySizeWeightPenalty. The weight penalty caches "
            + "(int)limit.ResultNumber as the true base the daily shed trims to, so scaling applied after "
            + "it leaves the shed trimming to the unscaled limit and the feature no-ops silently.");
    }

    [TestMethod]
    public void PartySizeModel_AppliesTheCaravanCapBeforeTheTroopWeightEliteTax()
    {
        // Same trap as the lord scaling above, same consequence. ApplyPartySizeWeightPenalty
        // snapshots (int)limit.ResultNumber as the party's true base, so a caravan bonus applied
        // after it would leave the daily shed trimming to vanilla's 30-50 and the caravan would
        // bleed back down over a week. Both orderings produce an identical ExplainedNumber, so
        // nothing but a source-order assertion catches it.
        var src = ReadPartySizeModel();

        int caravan = src.IndexOf("_aiPartySize.ApplyCaravanScaling", System.StringComparison.Ordinal);
        int weightPenalty = src.IndexOf(
            "_troopWeight.ApplyPartySizeWeightPenalty", System.StringComparison.Ordinal);

        Assert.AreNotEqual(-1, caravan, "TaomPartySizeModel must call ApplyCaravanScaling");
        Assert.AreNotEqual(-1, weightPenalty, "TaomPartySizeModel must call ApplyPartySizeWeightPenalty");
        Assert.IsTrue(
            caravan < weightPenalty,
            "ApplyCaravanScaling must run BEFORE ApplyPartySizeWeightPenalty, or the daily shed "
            + "trims caravans back to the vanilla 30-50 cap and the parity templates spawn rosters "
            + "that bleed away with every unit test still green.");
    }

    [TestMethod]
    public void PartySizeModel_ScalesGarrisonsThroughTheGarrisonOverride()
    {
        var src = ReadPartySizeModel();

        StringAssert.Contains(
            src, "CalculateGarrisonPartySizeLimit",
            "Garrison scaling must go through the CalculateGarrisonPartySizeLimit override, which "
            + "base.GetPartyMemberSizeLimit dispatches to virtually for garrison parties.");
        StringAssert.Contains(src, "ApplyGarrisonScaling");
    }

    [TestMethod]
    public void FoodConsumptionModel_AppliesAiFoodRelief()
    {
        var path = Path.Combine(
            FindRepoRoot(), "Main", "Features", "CulturalFeats", "Models", "TaomFoodConsumptionModel.cs");
        Assert.IsTrue(File.Exists(path), $"TaomFoodConsumptionModel.cs not found at {path}");

        StringAssert.Contains(File.ReadAllText(path), "ApplyAiFoodRelief");
    }

    [TestMethod]
    public void WageModel_AppliesAiWageReliefButLeavesPerHeadWageAlone()
    {
        var path = Path.Combine(
            FindRepoRoot(), "Main", "Features", "TroopProgression", "Models", "TaomPartyWageModel.cs");
        Assert.IsTrue(File.Exists(path), $"TaomPartyWageModel.cs not found at {path}");
        var src = File.ReadAllText(path);

        StringAssert.Contains(src, "ApplyAiWageRelief");

        // Campaign.AverageWage is built from GetCharacterWage, and the garrison-donation math divides
        // PaymentLimit by it. Discounting per-head wage would inflate the number of troops the AI
        // thinks it can afford to leave behind, so the relief belongs on the party total only.
        int relief = src.IndexOf("ApplyAiWageRelief", System.StringComparison.Ordinal);
        int getTotalWage = src.IndexOf("GetTotalWage", System.StringComparison.Ordinal);
        Assert.IsTrue(relief > getTotalWage, "AI wage relief must be applied inside GetTotalWage");
    }
}

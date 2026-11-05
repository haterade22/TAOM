using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

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
    public void PartySizeModel_AppliesTheCareerFlatCountAfterEveryFactor_AndBeforeTheEliteTax()
    {
        // ApplyFlat divides the factors ALREADY on the number back out, so a factor added after it
        // (the AI lord multiplier) would multiply the flat count again. It must come after the last
        // AddFactor, and before the TroopWeight snapshot so that snapshot includes the career count.
        // Call tokens, not the bare word: the explanatory comment above the call contains it.
        var src = ReadSource("Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs", stripComments: true);

        int flat = src.IndexOf("_careerPassives.ApplyFlat(", System.StringComparison.Ordinal);
        int feats = src.IndexOf("_feats.ApplyPartySizeFeats(", System.StringComparison.Ordinal);
        int aiLord = src.IndexOf("_aiPartySize.ApplyAiLordScaling(", System.StringComparison.Ordinal);
        int caravan = src.IndexOf("_aiPartySize.ApplyCaravanScaling(", System.StringComparison.Ordinal);
        int weightPenalty = src.IndexOf("_troopWeight.ApplyPartySizeWeightPenalty(", System.StringComparison.Ordinal);

        Assert.AreNotEqual(-1, flat, "TaomPartySizeModel must call _careerPassives.ApplyFlat(");
        Assert.AreNotEqual(-1, aiLord, "TaomPartySizeModel must call _aiPartySize.ApplyAiLordScaling(");
        Assert.AreNotEqual(-1, caravan, "TaomPartySizeModel must call _aiPartySize.ApplyCaravanScaling(");
        Assert.AreNotEqual(-1, weightPenalty, "TaomPartySizeModel must call _troopWeight.ApplyPartySizeWeightPenalty(");
        Assert.AreNotEqual(-1, feats, "TaomPartySizeModel must call _feats.ApplyPartySizeFeats(");
        Assert.IsTrue(feats < flat,
            "ApplyFlat must run AFTER the culture feat AddFactor (ApplyPartySizeFeats, the factor most "
            + "players have), or the feat multiplies the career's flat count again.");
        Assert.IsTrue(flat > aiLord && flat > caravan,
            "ApplyFlat must run AFTER the AI lord AddFactor (ApplyAiLordScaling, which adds a factor and a "
            + "flat bonus) and after ApplyCaravanScaling (a flat result-frame bonus with no factor of its "
            + "own), or a later factor multiplies the career's flat count again.");
        Assert.IsTrue(flat < weightPenalty,
            "ApplyFlat must run BEFORE ApplyPartySizeWeightPenalty, whose snapshot must include the career count.");
    }

    [TestMethod]
    public void PartySizeModel_CareerPartySizePassive_KeysOnTheLeaderOnly()
    {
        var src = ReadSource("Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs", stripComments: true);

        StringAssert.Contains(
            src, "ApplyFlat(party?.LeaderHero?.StringId, ref result, PassiveEffectType.PartySize)",
            "The PartySize passive must key on party.LeaderHero, not CareerPassiveHero.ResolveId (#768). "
            + "Owner-first reverts a +50 pick to growing every party the owner's clan touches: the "
            + "owner's caravans, the garrison, militia, villager and patrol parties of the owner clan's "
            + "fiefs, and TAOM refuges and supply caravans.");
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

using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CaravanTrade;

/// <summary>
/// Drift-guards for the CaravanTrade feature's private-member bindings. The four
/// <c>[HarmonyPatch]</c> targets auto-enroll in <c>HarmonyPatchBindingTests</c>, but the two private
/// FieldRef targets (<c>_defaultCaravanVeryFarCache</c> / <c>_navalCaravanVeryFarCache</c>), the
/// distance helper called directly by the score hook, and the <c>DefaultCaravanModel</c> override
/// targets are not covered there. A rename on an engine bump would silently no-op (fields) or break
/// the build (helper); these tests catch a field/method rename offline first. Types resolved by name
/// (mirrors how Harmony binds), gated on the installed engine like the other binding tests.
/// </summary>
[TestClass]
public class CaravanTradeBindingTests
{
    private const string CaravanBehavior = "TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior";
    private const string DefaultCaravanModelName = "TaleWorlds.CampaignSystem.GameComponents.DefaultCaravanModel";
    private const string AiHelperName = "Helpers.AiHelper";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static System.Type RequireType(string name)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(name);
        Assert.IsNotNull(type, name + " did not resolve against the installed engine.");
        return type;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PrivateMethodTargets_Resolve_AgainstInstalledEngine()
    {
        var t = RequireType(CaravanBehavior);
        // GetDistanceLimitVeryFarAsDaysForNavigationType is the Lever 3 target (the live read-point for
        // the "very far" ceiling); the other three are Levers 1/2/4.
        foreach (var name in new[] { "CanTradeWith", "GetTradeScoreForTown", "GetDistanceLimitVeryFarAsDaysForNavigationType", "CalculateBudgetFactor" })
            Assert.IsNotNull(AccessTools.Method(t, name),
                $"CaravansCampaignBehavior.{name} did not resolve — a CaravanTrade postfix target drifted.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void DistanceHelper_Resolves_AgainstInstalledEngine()
    {
        var t = RequireType(AiHelperName);
        Assert.IsNotNull(
            AccessTools.Method(t, "GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty"),
            "AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty did not resolve — the score-reweight distance recompute would break.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CaravanModelOverrideTargets_Resolve_AgainstInstalledEngine()
    {
        var t = RequireType(DefaultCaravanModelName);
        Assert.IsNotNull(AccessTools.Method(t, "GetInitialTradeGold"),
            "DefaultCaravanModel.GetInitialTradeGold did not resolve — TaomCaravanModel's diversity override target is gone.");
        Assert.IsNotNull(AccessTools.Method(t, "GetMaxGoldToSpendOnOneItemCategory"),
            "DefaultCaravanModel.GetMaxGoldToSpendOnOneItemCategory did not resolve — TaomCaravanModel's diversity override target is gone.");
    }
}

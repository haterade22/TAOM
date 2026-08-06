using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.EconomyDiagnostics;

/// <summary>
/// Drift-guards for the `Patch68_EconomyDiagnostics` targets.
///
/// Two of the five are PRIVATE engine methods (`ItemConsumptionBehavior.UpdateTownGold` and
/// `.MakeConsumption`). A rename on an engine bump would leave the flow tag unset and every mint and
/// resident-consumption movement would silently land in `Other` — a ledger that still looks
/// plausible while attributing the town's entire income to "unknown". That is worse than a crash,
/// so it gets an offline guard.
///
/// The recorder's own target, `SettlementComponent.ChangeGold`, is load-bearing in a different way:
/// it is the single mutator of the pool, and the claim "no flow site can be missed" holds only for
/// as long as that stays true.
/// </summary>
[TestClass]
public class EconomyDiagnosticsBindingTests
{
    private const string ItemConsumption = "TaleWorlds.CampaignSystem.CampaignBehaviors.ItemConsumptionBehavior";
    private const string SettlementComponentName = "TaleWorlds.CampaignSystem.Settlements.SettlementComponent";
    private const string SellGoodsForTrade = "TaleWorlds.CampaignSystem.Actions.SellGoodsForTradeAction";
    private const string SellItems = "TaleWorlds.CampaignSystem.Actions.SellItemsAction";

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
    public void PrivateFlowTagTargets_Resolve_AgainstInstalledEngine()
    {
        var t = RequireType(ItemConsumption);

        foreach (var name in new[] { "UpdateTownGold", "MakeConsumption" })
            Assert.IsNotNull(AccessTools.Method(t, name),
                $"ItemConsumptionBehavior.{name} did not resolve — that flow would silently record as Other.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PublicFlowTagTargets_Resolve_AgainstInstalledEngine()
    {
        Assert.IsNotNull(
            AccessTools.Method(RequireType(SellGoodsForTrade), "ApplyByVillagerTrade"),
            "SellGoodsForTradeAction.ApplyByVillagerTrade drifted — villager deliveries would record as Other.");

        Assert.IsNotNull(
            AccessTools.Method(RequireType(SellItems), "Apply"),
            "SellItemsAction.Apply drifted — all AI trade would record as Other.");
    }

    /// <summary>
    /// The ledger's central claim is that this is the ONLY mutator of settlement gold, which is what
    /// makes "no flow can be missed" true. If the engine ever grows a second write path this test
    /// will not catch it — but a rename or signature change here breaks the recorder outright, and
    /// that must fail offline rather than in a save.
    /// </summary>
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RecorderTarget_ChangeGold_ResolvesAndStillTakesASingleInt()
    {
        var method = AccessTools.Method(RequireType(SettlementComponentName), "ChangeGold");

        Assert.IsNotNull(method, "SettlementComponent.ChangeGold did not resolve — the gold recorder is dead.");

        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, "ChangeGold's signature changed — re-verify the recorder.");
        Assert.AreEqual(typeof(int), parameters[0].ParameterType);
    }

    /// <summary>
    /// `Gold` must stay readable for the prefix/postfix delta, and the property is what the engine
    /// hard-floors at zero — the clamp the recorder exists to make visible.
    /// </summary>
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RecorderTarget_GoldProperty_IsStillReadable()
    {
        var property = AccessTools.Property(RequireType(SettlementComponentName), "Gold");

        Assert.IsNotNull(property, "SettlementComponent.Gold did not resolve.");
        Assert.IsNotNull(property.GetGetMethod(), "SettlementComponent.Gold lost its public getter.");
    }
}

using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// Drift-guard for SupplyLines' engine bindings that no Harmony patch covers. The caravan movement
/// drives <c>MobileParty.Bearing</c> through its non-public setter via reflection (resolved once in
/// <c>SupplyCaravanService</c>), and the source service calls two model methods and one factory by
/// compiled signature; if any of these drifts on an engine bump, this suite fails in CI instead of
/// caravans silently sliding sideways or the recruit screen going empty.
/// </summary>
[TestClass]
public class SupplyCaravanBearingBindingTests
{
    private const string MobilePartyTypeName = "TaleWorlds.CampaignSystem.Party.MobileParty";
    private const string PartyComponentTypeName = "TaleWorlds.CampaignSystem.Party.PartyComponents.PartyComponent";
    private const string VolunteerModelTypeName = "TaleWorlds.CampaignSystem.ComponentInterfaces.VolunteerModel";
    private const string PartyWageModelTypeName = "TaleWorlds.CampaignSystem.ComponentInterfaces.PartyWageModel";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static System.Type RequireType(string typeName)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(typeName);
        Assert.IsNotNull(type, typeName + " did not resolve against the installed engine.");
        return type;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BearingSetter_BindingResolves_AgainstInstalledEngine()
    {
        var mobilePartyType = RequireType(MobilePartyTypeName);

        var setter = AccessTools.PropertySetter(mobilePartyType, "Bearing");
        Assert.IsNotNull(
            setter,
            "MobileParty.Bearing setter did not resolve; SupplyCaravanService.SetBearing would "
            + "log once and caravan icons would stop facing their travel direction.");
        Assert.AreEqual(
            "TaleWorlds.Library.Vec2",
            setter.GetParameters().Single().ParameterType.FullName,
            "MobileParty.Bearing changed type; the reflection invoke would throw on every frame.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MaximumIndexHeroCanRecruitFromHero_Resolves_AgainstInstalledEngine()
    {
        var modelType = RequireType(VolunteerModelTypeName);

        var method = AccessTools.Method(modelType, "MaximumIndexHeroCanRecruitFromHero");
        Assert.IsNotNull(
            method,
            "VolunteerModel.MaximumIndexHeroCanRecruitFromHero did not resolve; SupplySourceService's "
            + "alignment gate on volunteer recruiting has no engine hook.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetTroopRecruitmentCost_Resolves_AgainstInstalledEngine()
    {
        var modelType = RequireType(PartyWageModelTypeName);

        var method = AccessTools.Method(modelType, "GetTroopRecruitmentCost");
        Assert.IsNotNull(
            method,
            "PartyWageModel.GetTroopRecruitmentCost did not resolve; supply troop pricing loses its "
            + "vanilla base cost.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CreateParty_StringAndComponentOverload_Resolves_AgainstInstalledEngine()
    {
        var mobilePartyType = RequireType(MobilePartyTypeName);
        var componentType = RequireType(PartyComponentTypeName);

        var method = AccessTools.Method(
            mobilePartyType, "CreateParty", new[] { typeof(string), componentType });
        Assert.IsNotNull(
            method,
            "MobileParty.CreateParty(string, PartyComponent) did not resolve; "
            + "SupplyCaravanService.Spawn cannot create caravan parties.");
    }
}

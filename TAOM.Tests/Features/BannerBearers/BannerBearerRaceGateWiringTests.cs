using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BannerBearers;

/// <summary>
/// A dropped banner is fetched by the nearest formation member that passes CanAgentPickUpAnyBanner, and the engine
/// asks nothing else (v1.4.8 BannerBearerLogic.FindBestSearcherForBanner). SandBox's and Custom Battle's answers
/// are race-blind, so every TAOM banner model adds the race gate there. Agent is sealed, so the gate is pinned in
/// source; the reflection rows prove each method overrides the engine's instead of shadowing it.
/// </summary>
[TestClass]
public class BannerBearerRaceGateWiringTests
{
    private const string CampaignModelSource = "Main/Features/BannerBearers/Models/TaomBattleBannerBearersModel.cs";
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type Taom(string fullName)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        return typeof(TAOM.IoC).Assembly.GetType(fullName, throwOnError: true)!;
    }

    [DataTestMethod]
    [DataRow(CampaignModelSource)]
    public void EachBannerModel_GatesTheDroppedBannerPickupByRace(string path)
    {
        var source = RepoPaths.ReadSource(path, stripComments: true);

        StringAssert.Contains(source, "public override bool CanAgentPickUpAnyBanner(Agent agent)");
        StringAssert.Contains(source, "base.CanAgentPickUpAnyBanner(agent) && _service.PassesRaceGate(agent?.Character?.Race ?? -1)");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampaignModel_OverridesTheEnginesPickupCheck()
    {
        var method = Taom("TAOM.Features.BannerBearers.Models.TaomBattleBannerBearersModel").GetMethod("CanAgentPickUpAnyBanner", Declared);

        Assert.IsNotNull(method, "TaomBattleBannerBearersModel does not declare CanAgentPickUpAnyBanner");
        Assert.AreNotSame(method, method!.GetBaseDefinition(), "it shadows the engine's method instead of overriding it");
    }
}

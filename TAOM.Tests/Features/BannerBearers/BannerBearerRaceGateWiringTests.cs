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
    private const string CustomModelSource = "Main/Features/BannerBearers/Models/TaomCustomBattleBannerBearersModel.cs";
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
    [DataRow(CustomModelSource)]
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

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CustomBattleModel_ExtendsTheEnginesCustomBattleModel_AndOverridesBothGates()
    {
        var type = Taom("TAOM.Features.BannerBearers.Models.TaomCustomBattleBannerBearersModel");
        Assert.AreEqual("TaleWorlds.MountAndBlade.CustomBattleBannerBearersModel", type.BaseType!.FullName);
        foreach (var name in new[] { "CanAgentBecomeBannerBearer", "CanAgentPickUpAnyBanner" })
        {
            var method = type.GetMethod(name, Declared);
            Assert.IsNotNull(method, name + " is not declared");
            Assert.AreNotSame(method, method!.GetBaseDefinition(), name + " shadows instead of overriding");
        }
    }

    [TestMethod]
    public void RegisterCustomBattleModels_AddsTheRaceGateOnABasicGameStarterOnly()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var start = src.IndexOf("private static void RegisterCustomBattleModels(IGameStarter gameStarterObject)", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "RegisterCustomBattleModels is missing");
        var body = src.Substring(start, src.IndexOf("private static void", start + 20, StringComparison.Ordinal) - start);

        StringAssert.Contains(body, "if (gameStarterObject is CampaignGameStarter || !(gameStarterObject is BasicGameStarter basicStarter))");
        StringAssert.Contains(body, "basicStarter.AddModel<BattleBannerBearersModel>(new TaomCustomBattleBannerBearersModel(");
    }

    [TestMethod]
    public void RegisterCustomBattleModels_RunsBeforeTheCampaignBranchAndTheModuleStep()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var call = src.IndexOf("RegisterCustomBattleModels(gameStarterObject);", StringComparison.Ordinal);
        var campaign = src.IndexOf("if (gameStarterObject is CampaignGameStarter campaignStarter)", StringComparison.Ordinal);
        var modules = src.IndexOf("FeatureModuleHooks.AddGameStartContent(gameStarterObject);", StringComparison.Ordinal);

        Assert.IsTrue(call >= 0 && call < campaign && campaign < modules,
            "the Custom Battle step must precede the campaign branch, and the module step must come last (the last model added wins)");
    }
}

using System;
using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Hooks;
using TAOM.Features.CareerSystem;
using TAOM.Features.CoopInterop;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.SpecialResources;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// Wiring guards. The module owns save data, so campaign start is fail-closed on it: one dependency its four
/// behaviors cannot resolve throws out of OnGameStart and no campaign starts or loads. CultureMarketplace's
/// behavior takes the stock gate only this module registers. And SubModule must apply the gate on every
/// game init, above the once-per-process guard, or a second game in one process keeps the first game's
/// classes while its items reload their XML flags (the MonsterSizeWiringTests shape).
/// </summary>
[TestClass]
public class ArmourAcquisitionWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheArmourAcquisitionModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<ArmourAcquisitionModule>().Count(),
            "ArmourAcquisitionModule must be listed exactly once in Main/Composition/FeatureModules.cs");
    }

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("RegisterArmourAcquisitionFeature"),
            "Main/IoC.cs registers the feature by hand AND through its module: every service gets a second default registration");
    }

    [TestMethod]
    public void OnGameInitializationFinished_AppliesTheGate_BeforeTheOncePerProcessGuard()
    {
        var sub = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        int method = sub.IndexOf("public override void OnGameInitializationFinished(Game game)", StringComparison.Ordinal);
        Assert.IsTrue(method >= 0, "OnGameInitializationFinished is gone from SubModule.cs");
        int call = sub.IndexOf("IoC.Resolve<Features.ArmourAcquisition.IArmourGateService>().ApplyGating(", method, StringComparison.Ordinal);
        int guard = sub.IndexOf("if (_gameInitPatchesApplied) return;", method, StringComparison.Ordinal);

        Assert.IsTrue(call > method, "OnGameInitializationFinished no longer applies the armour gate");
        Assert.IsTrue(guard > call, "the gate must be applied before the once-per-process guard: each game reloads its items");
    }

    [TestMethod]
    public void SpecialResourceService_IsTheSpender_SoTheDelegateRegistrationCastHolds()
    {
        // SpecialResourcesIoC registers ISpecialResourceSpender as a cast of the ISpecialResourceService singleton.
        Assert.IsTrue(typeof(ISpecialResourceSpender).IsAssignableFrom(typeof(SpecialResourceService)));
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndEveryBehaviorDeclResolves()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.Combine(Path.GetTempPath(), "taom-armour-wiring-" + Guid.NewGuid().ToString("N")));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ITownRosterAdapter>());
        container.RegisterInstance(Substitute.For<ICareerQuestService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<ICultureMarketplaceConfigProvider>());
        container.RegisterInstance(Substitute.For<ISpecialResourceSpender>());
        var module = new ArmourAcquisitionModule();

        module.RegisterServices(container);

        Assert.IsTrue(module.OwnsSaveData);
        var types = module.CampaignBehaviors.Select(d => d.BehaviorType).ToList();
        CollectionAssert.AreEquivalent(new[]
        {
            typeof(ArmourAcquisitionCampaignBehavior), typeof(ArmouryMenuBehavior), typeof(LordsLadderBehavior),
            typeof(LordHarnessEventBehavior),
        }, types);
        CollectionAssert.AreEqual(new[] { typeof(HeroKillCounterMissionLogic) },
            module.MissionBehaviors.Select(d => d.BehaviorType).ToArray(), "the ladder's kill counter joins every mission");
        foreach (var decl in module.CampaignBehaviors)
            Assert.IsInstanceOfType(decl.Create(container), decl.BehaviorType);
        // The runner starts it with failClosed false, so a factory that throws would silently drop the counter.
        Assert.IsInstanceOfType(module.MissionBehaviors.Single().Create(null!, container), typeof(HeroKillCounterMissionLogic));
        Assert.IsInstanceOfType(container.Resolve<IMarketplaceStockGate>(), typeof(ArmourMarketplaceGate));
        Assert.IsNotNull(container.Resolve<IArmourGateService>());
    }
}

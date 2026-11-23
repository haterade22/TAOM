using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.BattleGearLoot;
using TAOM.Features.BattleGearLoot.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Features.TroopProgression;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BattleGearLoot;

[TestClass]
public class BattleGearLootWiringTests
{
    private static Container BuildContainer(BattleGearLootModule module)
    {
        var container = new Container();
        module.RegisterServices(container);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IOutcomeLog>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IArmourAcquisitionConfigProvider>());
        container.RegisterInstance(Substitute.For<IRandomProvider>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<BattleGearLootModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.BattleGearLoot.BattleGearLootModule()"));
    }

    [TestMethod]
    public void TheModule_OwnsNoSaveData_AndAppliesOneGameInitCategory()
    {
        var module = new BattleGearLootModule();

        Assert.AreEqual("BattleGearLoot", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(1, module.PatchCategories.Count);
        Assert.AreEqual("Patch172_BattleGearLoot", module.PatchCategories[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, module.PatchCategories[0].Phase);
    }

    [TestMethod]
    public void TheModule_RegistersTheServiceAndItsSettingsProvider()
    {
        var container = BuildContainer(new BattleGearLootModule());

        Assert.IsNotNull(container.Resolve<BattleGearLootService>());
        Assert.IsInstanceOfType(container.Resolve<IBattleGearLootSettingsProvider>(), typeof(BattleGearLootSettingsProvider));
    }

    [TestCategory("RequiresGame")]   // constructs ItemRoster
    [TestMethod]
    public void InitializeStatics_GivesThePatchItsService_AndTheSwitchOffLeavesVanilla()
    {
        var module = new BattleGearLootModule();
        var container = BuildContainer(module);

        module.InitializeStatics(container);
        try
        {
            Assert.IsTrue(Patch172_BattleGearLoot.Prefix(null!, null!, null!, 1, new TaleWorlds.CampaignSystem.Roster.ItemRoster()));
        }
        finally
        {
            Patch172_BattleGearLoot.Initialize(null, null);
        }
    }
}

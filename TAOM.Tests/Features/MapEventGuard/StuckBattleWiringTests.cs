using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.MapEventGuard;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>
/// Wiring guard for the stuck AI battle sweep (#748): listed once, one campaign behavior, no patch and no saved
/// data, and a fresh DryIoc container can build the service over the real adapter from the dependencies the
/// hand-wired features register first.
/// </summary>
[TestClass]
public class StuckBattleWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<StuckBattleModule>().Count(),
            "StuckBattleModule must be listed exactly once in Main/Composition/FeatureModules.cs.");
    }

    [TestMethod]
    public void Module_DeclaresOneBehaviorAndNoPatchOrSaveData()
    {
        var module = new StuckBattleModule();

        Assert.AreEqual("StuckBattle", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void Module_RegistersAServiceTheContainerCanBuild()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        new StuckBattleModule().RegisterServices(container);

        Assert.IsNotNull(container.Resolve<StuckBattleService>());
    }
}

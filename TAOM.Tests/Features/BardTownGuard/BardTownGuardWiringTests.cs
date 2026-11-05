using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.BardTownGuard;

namespace TAOM.Tests.Features.BardTownGuard;

/// <summary>Wiring guard for the tavern musician fix (#824): drop the module line and the crash returns.</summary>
[TestClass]
public class BardTownGuardWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheBardTownGuardModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<BardTownGuardModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedBardTownGuard_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new BardTownGuardModule();

        Assert.AreEqual("BardTownGuard", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesPatch110AtGameInit_AndOnlyThat()
    {
        var decls = new BardTownGuardModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch110_BardTownGuard", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModel()
    {
        var module = new BardTownGuardModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }
}

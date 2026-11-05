using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.TavernSpecialities;

namespace TAOM.Tests.Features.TavernSpecialities;

/// <summary>Wiring guard for the tavern maid fix (#937): drop the module line and the stale dishes return.</summary>
[TestClass]
public class TavernSpecialitiesWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheTavernSpecialitiesModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<TavernSpecialitiesModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedTavernSpecialities_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new TavernSpecialitiesModule();

        Assert.AreEqual("TavernSpecialities", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesPatch111AtGameInit_AndOnlyThat()
    {
        var decls = new TavernSpecialitiesModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch111_TavernSpecialities", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModel()
    {
        var module = new TavernSpecialitiesModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }
}

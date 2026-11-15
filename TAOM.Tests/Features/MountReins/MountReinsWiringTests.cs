using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.MountReins;
using TAOM.Features.MountReins.Hooks;

namespace TAOM.Tests.Features.MountReins;

/// <summary>Wiring guard for the mount reins guard (Patch153): drop the module line and the mounted spawn crash returns.</summary>
[TestClass]
public class MountReinsWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheMountReinsModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<MountReinsModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedMountReins_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new MountReinsModule();

        Assert.AreEqual("MountReins", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesPatch153AtGameInit_AndOnlyThat()
    {
        var decls = new MountReinsModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch153_MountReinsGuard", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModel()
    {
        var module = new MountReinsModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void ThePatchClass_IsClientOnly()
        => Assert.IsTrue(typeof(Patch153_MountReinsGuard).IsDefined(typeof(TAOM.ClientOnlyAttribute), false),
            "MountVisualCreator lives in TaleWorlds.MountAndBlade.View, which a dedicated server does not load");
}

using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.ButterLibDistanceMatrix;

namespace TAOM.Tests.Features.ButterLibDistanceMatrix;

/// <summary>
/// Wiring guard for the ButterLib Distance Matrix switch (#740): listed once, nothing but a main-menu side effect, and a
/// fresh DryIoc container can build the switch over the real adapter (DryIoc picks only a public constructor, here as in the game's
/// container, so a non-public one fails this test).
/// </summary>
[TestClass]
public class ButterLibDistanceMatrixWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<ButterLibDistanceMatrixModule>().Count(),
            "ButterLibDistanceMatrixModule must be listed exactly once in Main/Composition/FeatureModules.cs.");
    }

    [TestMethod]
    public void Module_DeclaresNoPatchBehaviorOrSaveData()
    {
        var module = new ButterLibDistanceMatrixModule();

        Assert.AreEqual("ButterLibDistanceMatrix", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void Module_RegistersAServiceTheContainerCanBuild()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        new ButterLibDistanceMatrixModule().RegisterServices(container);

        Assert.IsNotNull(container.Resolve<DistanceMatrixSwitch>());
    }

    // MainMenu is the design: MCM applies its stored ButterLib toggle in its own first main-menu hook, before TAOM's,
    // so a switch at ProcessLoad would be undone.
    [TestMethod]
    public void OnPhase_SwitchesOnlyAtMainMenu()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        var module = new ButterLibDistanceMatrixModule();
        module.RegisterServices(container);
        var adapter = Substitute.For<IButterLibDistanceMatrixAdapter>();
        container.RegisterInstance(adapter, IfAlreadyRegistered.Replace);

        module.OnPhase(ApplyPhase.ProcessLoad, container);
        module.OnPhase(ApplyPhase.GameInit, container);
        module.OnPhase(ApplyPhase.FirstMission, container);
        adapter.DidNotReceive().TryDisable(out NSubstitute.Arg.Any<bool>());

        module.OnPhase(ApplyPhase.MainMenu, container);
        adapter.Received(1).TryDisable(out NSubstitute.Arg.Any<bool>());
    }
}

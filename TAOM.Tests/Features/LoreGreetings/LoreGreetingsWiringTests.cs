using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.LoreGreetings;
using TAOM.Features.LoreGreetings.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LoreGreetings;

/// <summary>Wiring guard for Lore Greetings (Patch177): drop the module line and every greeting goes back to what it was, with every other test green.</summary>
[TestClass]
public class LoreGreetingsWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<LoreGreetingsModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedLoreGreetings_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new LoreGreetingsModule();

        Assert.AreEqual("LoreGreetings", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesPatch177AtGameInit_AndOnlyThat()
    {
        var decls = new LoreGreetingsModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch177_LoreGreetings", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void ThePatchCategory_IsDeclaredByNoOtherModule()
        => Assert.AreEqual(1, FeatureModules.All.SelectMany(m => m.PatchCategories).Count(d => d.Category == LoreGreetingsModule.PatchCategory));

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModelAndNoMissionBehavior()
    {
        var module = new LoreGreetingsModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    private static Container Registered()
    {
        var c = new Container();
        new LoreGreetingsModule().RegisterServices(c);
        c.RegisterInstance(Substitute.For<IModLogger>());
        c.RegisterInstance(Substitute.For<IOutcomeLog>());
        c.RegisterInstance(Substitute.For<IAlignmentService>());
        c.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        c.RegisterInstance(Substitute.For<IPathService>());
        return c;
    }

    [TestMethod]
    public void RegisterServices_ResolvesTheService_AsOneSingleton()
    {
        using var c = Registered();

        Assert.AreSame(c.Resolve<LoreGreetingsService>(), c.Resolve<LoreGreetingsService>());
        Assert.IsInstanceOfType(c.Resolve<ILoreGreetingsAdapter>(), typeof(LoreGreetingsAdapter));
        Assert.IsInstanceOfType(c.Resolve<ILoreGreetingsConfigProvider>(), typeof(LoreGreetingsConfigProvider));
    }

    [TestMethod]
    public void InitializeStatics_HandsTheServiceToBothPatches()
    {
        using var c = Registered();

        new LoreGreetingsModule().InitializeStatics(c);

        Assert.AreSame(c.Resolve<LoreGreetingsService>(), Patch177_LoreGreeting.Service);
        Assert.AreSame(c.Resolve<LoreGreetingsService>(), Patch177_LoreMeeting.Service);
    }

    [TestMethod]
    public void NothingIsWiredInIoCOrSubModule()
    {
        Assert.IsFalse(RepoPaths.MentionsOutsideComments(RepoPaths.RepoPath("Main", "IoC.cs"), "LoreGreetings"));
        Assert.IsFalse(RepoPaths.MentionsOutsideComments(RepoPaths.RepoPath("Main", "SubModule.cs"), "LoreGreetings"));
    }
}

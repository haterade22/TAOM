using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BanditVoices;
using TAOM.Features.CoopInterop;
using TAOM.Features.EncounterLines;
using TAOM.Features.EncounterLines.Hooks;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>Wiring guard for Encounter Lines (Patch149): drop the module line and every encounter goes back to vanilla, with every other test green.</summary>
[TestClass]
public class EncounterLinesWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<EncounterLinesModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedEncounterLines_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new EncounterLinesModule();

        Assert.AreEqual("EncounterLines", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesPatch149AtGameInit_AndOnlyThat()
    {
        var decls = new EncounterLinesModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch149_EncounterLines", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void ThePatchCategory_IsDeclaredByNoOtherModule()
        => Assert.AreEqual(1, FeatureModules.All.SelectMany(m => m.PatchCategories).Count(d => d.Category == "Patch149_EncounterLines"));

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModelAndNoMissionBehavior()
    {
        var module = new EncounterLinesModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    private static Container Registered(bool withBanditVoices = false)
    {
        var c = new Container();
        new EncounterLinesModule().RegisterServices(c);
        if (withBanditVoices)
            new BanditVoicesModule().RegisterServices(c);
        c.RegisterInstance(Substitute.For<IModLogger>());
        c.RegisterInstance(Substitute.For<IRandomSource>());
        c.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        c.RegisterInstance(Substitute.For<IMessengerConversationState>());
        c.RegisterInstance(Substitute.For<IAlignmentService>());
        c.RegisterInstance(Substitute.For<IRaceManager>());
        c.RegisterInstance(Substitute.For<IPathService>());
        return c;
    }

    [TestMethod]
    public void RegisterServices_MakesTheServiceAndTheAdapterSingletons_SharedByEveryInterface()
    {
        using var c = Registered();

        Assert.AreSame(c.Resolve<EncounterLinesService>(), c.Resolve<EncounterLinesService>());
        Assert.AreSame(c.Resolve<EncounterLinesService>(), c.Resolve<IEncounterBucketSource>());
        Assert.AreSame(c.Resolve<EncounterLinesAdapter>(), c.Resolve<IEncounterLinesAdapter>());
        Assert.IsInstanceOfType(c.Resolve<IEncounterLinesSettingsProvider>(), typeof(EncounterLinesSettingsProvider));
        Assert.IsInstanceOfType(c.Resolve<IEncounterGroupsConfigProvider>(), typeof(EncounterGroupsConfigProvider));
    }

    [TestMethod]
    public void InitializeStatics_HandsTheServiceToTheGateAndTheFourPatches_AndCanRunTwice()
    {
        using var c = Registered();
        var module = new EncounterLinesModule();

        module.InitializeStatics(c);
        module.InitializeStatics(c);

        var service = c.Resolve<EncounterLinesService>();
        Assert.AreSame(service, EncounterTagGate.Service);
        Assert.AreSame(service, CaravanGreetingPatch.Service);
        Assert.AreSame(service, VillagerGreetingPatch.Service);
        Assert.AreSame(service, RobberyLinesPatch.Service);
        Assert.AreSame(service, EncounterSentenceTextPatch.Service);
    }

    [TestMethod]
    public void BanditVoices_ResolvesWithAndWithoutEncounterLines()
    {
        using (var both = Registered(withBanditVoices: true))
        {
            var withBuckets = both.Resolve<BanditVoicesService>();
            Assert.IsNotNull(withBuckets);
            Assert.AreSame(both.Resolve<IEncounterBucketSource>(), BucketSourceOf(withBuckets), "Bandit Voices did not receive Encounter Lines' bucket source");
        }

        using var alone = new Container();
        new BanditVoicesModule().RegisterServices(alone);
        alone.RegisterInstance(Substitute.For<IModLogger>());
        alone.RegisterInstance(Substitute.For<IRandomSource>());
        alone.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        alone.RegisterInstance(Substitute.For<IMessengerConversationState>());
        var withoutBuckets = alone.Resolve<BanditVoicesService>();
        Assert.IsNotNull(withoutBuckets, "the bucket source is optional");
        Assert.IsNull(BucketSourceOf(withoutBuckets));
    }

    private static object? BucketSourceOf(BanditVoicesService service)
    {
        var field = typeof(BanditVoicesService).GetField("_buckets", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field, "BanditVoicesService._buckets is gone; update this test");
        return field!.GetValue(service);
    }

    [TestMethod]
    public void TheModule_IsNotRegisteredByHandInIoC()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("Patch149") || src.Contains("EncounterLines"), "Main/IoC.cs must not name the module");
    }

    [TestMethod]
    public void TheCategory_IsNotAppliedByHandInSubModule()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("Patch149") || src.Contains("EncounterLines"), "Main/SubModule.cs must not name the module");
    }
}

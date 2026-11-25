using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.TournamentRewards;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>
/// Wiring guards. The wave fight owns save data (the day each town last ran a wave) and no patch and no model; slice 2 declares one
/// dialog behavior built with <c>new</c> for every campaign, registered through its module and nowhere else. The mission logic is pinned by
/// <c>TrainingWavesMissionWiringTests</c>.
/// </summary>
[TestClass]
public class TrainingWavesWiringTests
{
    private const string Folder = "Main/Features/TrainingGrounds/Waves";

    private static string Source(string relative) => RepoPaths.ReadSource(relative, stripComments: true);

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<TrainingWavesModule>().Count());
        Assert.AreEqual(1, Regex.Matches(Source("Main/Composition/FeatureModules.cs"), Regex.Escape("new Features.TrainingGrounds.Waves.TrainingWavesModule()")).Count);
    }

    [TestMethod]
    public void TheSingleOwnerFiles_NameNothingOfTheFeature()
    {
        foreach (var file in new[] { "Main/IoC.cs", "Main/SubModule.cs" })
        {
            Assert.IsFalse(Source(file).Contains("TrainingWaves"), file + " wires the feature by hand AND through its module");
            Assert.IsFalse(Source(file).Contains("TrainingGrounds.Waves"), file);
        }
    }

    [TestMethod]
    public void TheModule_OwnsSaveData_AndAddsOneCampaignBehavior_AndOneMissionBehavior()
    {
        var module = new TrainingWavesModule();

        Assert.AreEqual("TrainingWaves", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsTrue(module.OwnsSaveData);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(1, module.MissionBehaviors.Count, "the mission logic that runs the waves (TrainingWavesMissionWiringTests)");
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorWithNew_NeverFromTheContainer()
    {
        var src = Source(Folder + "/TrainingWavesModule.cs");

        Assert.IsTrue(src.Contains("new TrainingWavesDialogBehavior("));
        Assert.IsFalse(src.Contains("Resolve<TrainingWavesDialogBehavior>"));
        Assert.IsFalse(Source(Folder + "/TrainingWavesIoC.cs").Contains("TrainingWavesDialogBehavior"));
    }

    [TestMethod]
    public void TheBehaviorsConstructor_ResetsTheSession_BeforeAnythingCanReadTheState()
    {
        var src = Source(Folder + "/Hooks/TrainingWavesDialogBehavior.cs");
        var ctor = Regex.Match(src, @"public TrainingWavesDialogBehavior\([^)]*\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);

        Assert.IsTrue(ctor.Success);
        StringAssert.Contains(ctor.Groups["body"].Value, "ResetForNewSession()");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheBehavior_IsANewInstanceForEachCampaign()
    {
        var module = new TrainingWavesModule();
        using var container = Container();
        module.RegisterServices(container);

        var first = module.CampaignBehaviors[0].Create(container);
        var second = module.CampaignBehaviors[0].Create(container);

        Assert.IsInstanceOfType(first, typeof(TrainingWavesDialogBehavior));
        Assert.AreNotSame(first, second);
    }

    private static Container Container()
    {
        var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.Combine(Path.GetTempPath(), "taom-tgw-wiring-" + Guid.NewGuid().ToString("N")));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IOutcomeLog>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<ICareerPurseAdapter>());
        container.RegisterInstance(Substitute.For<ITournamentRewardsSettingsProvider>());
        return container;
    }

    [TestMethod]
    public void TheServiceGraph_ResolvesAgainstTheSharedRegistrations()
    {
        using var container = Container();
        new TrainingWavesModule().RegisterServices(container);

        Assert.AreSame(container.Resolve<WaveBetService>(), container.Resolve<WaveBetService>());
        Assert.IsInstanceOfType(container.Resolve<ITrainingWavesAdapter>(), typeof(TrainingWavesAdapter));
        Assert.IsInstanceOfType(container.Resolve<ITrainingWavesConfigProvider>(), typeof(TrainingWavesConfigProvider));
        Assert.IsInstanceOfType(container.Resolve<ITrainingWavesSettingsProvider>(), typeof(TrainingWavesSettingsProvider));
    }

    [TestMethod]
    public void TheIoC_RegistersNothingTheOtherFeaturesOwn()
    {
        var src = Source(Folder + "/TrainingWavesIoC.cs");

        foreach (var shared in new[] { "ICareerPurseAdapter", "ITournamentRewardsSettingsProvider", "ICoopSessionProvider", "IDedicatedServerProvider", "IOutcomeLog", "IPathService" })
            Assert.IsFalse(src.Contains("Register<" + shared) || src.Contains("<" + shared + ","), shared + " is registered by another feature");
    }

    [TestMethod]
    public void ThePurseAdapter_IsRegisteredByTheCareerSystem_ReachableFromAModulesResolver()
    {
        Assert.IsTrue(Source("Main/Features/CareerSystem/CareerSystemIoC.cs").Contains("Register<ICareerPurseAdapter, CareerPurseAdapter>"));
        Assert.IsTrue(Source("Main/IoC.cs").Contains("CareerSystemIoC.RegisterCareerSystemFeature(container)"));
        Assert.IsTrue(Source("Main/IoC.cs").Contains("Register<IOutcomeLog, OutcomeLog>"));
    }

    [TestMethod]
    public void TheService_HasNoInterface_ItHasOneImplementationAndTheTestsFakeItsSeams()
    {
        Assert.IsNull(typeof(WaveBetService).Assembly.GetType("TAOM.Features.TrainingGrounds.Waves.IWaveBetService"));
        Assert.IsFalse(File.Exists(RepoPaths.RepoPath("Main", "Features", "TrainingGrounds", "Waves", "IWaveBetService.cs")));
    }

    [TestMethod]
    public void TheBehaviorsCallbacks_AllRunThroughTheFaultWrapper()
    {
        var src = Source(Folder + "/Hooks/TrainingWavesDialogBehavior.cs");

        Assert.IsTrue(src.Contains("new ScreenFaultLog(logger, \"TrainingWaves\")"));
        foreach (var call in new[] { "_bets.ShowsOffer", "_bets.PlaceStake(", "_bets.Withdraw()", "_bets.TakeDebrief()", "_bets.OnConversationEnded()" })
            Assert.IsTrue(src.Contains(call), call);
    }
}

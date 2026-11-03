using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Spike;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
public class LongDefeatModuleTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static Container Registered()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        new LongDefeatModule().RegisterServices(container);
        return container;
    }

    [TestMethod]
    public void Module_ListedOnceInTheFeatureModules()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<LongDefeatModule>().Count());
    }

    [TestMethod]
    public void Module_DeclaresNothingThatRunsInAnotherMission()
    {
        var module = new LongDefeatModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void RegisterServices_EachServiceResolvesAsASingleton()
    {
        var container = Registered();

        Assert.AreSame(container.Resolve<ILongDefeatGameManagerFactory>(), container.Resolve<ILongDefeatGameManagerFactory>());
        Assert.AreSame(container.Resolve<LongDefeatSpikeLauncher>(), container.Resolve<LongDefeatSpikeLauncher>());
    }

    [TestMethod]
    public void TheSpikeLogic_AnswersTeamPower_BecauseTheSiegeTacticsReadItWithNoNullCheck()
    {
        Assert.IsTrue(typeof(IBattlePowerCalculationLogic).IsAssignableFrom(typeof(LongDefeatSpikeLogic)));
    }

    [TestMethod]
    public void TheLauncher_LeavesOutTheLogicThatNeedsTheSpawnLogic()
    {
        var source = RepoPaths.ReadSource("Main/Features/LongDefeat/Spike/LongDefeatSpikeLauncher.cs");

        Assert.IsFalse(source.Contains("new GeneralsAndCaptainsAssignmentLogic"), "it throws in OnInitialSpawnCompleted without DefaultBattleMissionAgentSpawnLogic");
    }

    [TestMethod]
    public void TheCoreFactory_IsTheNullOne_AndCreatesNothing()
    {
        var factory = Registered().Resolve<ILongDefeatGameManagerFactory>();

        Assert.IsInstanceOfType(factory, typeof(NullLongDefeatGameManagerFactory));
        Assert.IsNull(factory.Create(new LongDefeatSpikeOptions()));
    }
}

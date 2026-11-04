using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Core.Infrastructure;
using TAOM.Adapters;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Hooks;
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
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IOutcomeLog>());
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
        Assert.AreSame(container.Resolve<LongDefeatConfigCatalog>(), container.Resolve<LongDefeatConfigCatalog>());
        Assert.AreSame(container.Resolve<LongDefeatSession>(), container.Resolve<LongDefeatSession>());
        Assert.AreSame(container.Resolve<ILongDefeatMenuAdapter>(), container.Resolve<ILongDefeatMenuAdapter>());
        Assert.AreSame(container.Resolve<LongDefeatSetupPrompt>(), container.Resolve<LongDefeatSetupPrompt>());
        Assert.AreSame(container.Resolve<LongDefeatMissionLauncher>(), container.Resolve<LongDefeatMissionLauncher>());
    }

    [TestMethod]
    public void ThePhase0Spike_IsGoneFromSource()
    {
        Assert.IsFalse(Directory.Exists(RepoPaths.RepoPath("Main", "Features", "LongDefeat", "Spike")), "Phase 1 replaced the spike folder");

        var leftovers = new[] { "Main", "Client" }
            .SelectMany(root => Directory.EnumerateFiles(RepoPaths.RepoPath(root), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Where(path => File.ReadAllText(path).Contains("LongDefeatSpike") || File.ReadAllText(path).Contains("LONGDEFEAT_SPIKE"))
            .Select(path => Path.GetFileName(path))
            .ToList();

        Assert.AreEqual(0, leftovers.Count, "still naming the spike: " + string.Join(", ", leftovers));
    }

    [TestMethod]
    public void TheCoreFactory_IsTheNullOne_AndCreatesNothing()
    {
        var factory = Registered().Resolve<ILongDefeatGameManagerFactory>();

        Assert.IsInstanceOfType(factory, typeof(NullLongDefeatGameManagerFactory));
        Assert.IsFalse(factory.Start(new LongDefeatSetup("helms", "test", TAOM.Features.LongDefeat.Domain.RunOptions.Default)));
    }

    [TestMethod]
    public void OnPhase_MainMenu_AddsTheMenuOptionAndRepeatingIsSafe()
    {
        var container = Registered();
        var menu = new FakeLongDefeatMenu();
        container.RegisterInstance<ILongDefeatMenuAdapter>(menu, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance(Substitute.For<TAOM.Features.CoopInterop.IDedicatedServerProvider>());
        MainMenuStep.ResetForTests();
        var module = new LongDefeatModule();

        module.OnPhase(ApplyPhase.MainMenu, container);
        module.OnPhase(ApplyPhase.MainMenu, container);

        Assert.AreEqual(1, menu.AddCalls);
        Assert.IsTrue(menu.HasOption("TaomLongDefeat"));
    }

    [TestMethod]
    public void OnPhase_AnyOtherPhase_AddsNothing()
    {
        var container = Registered();
        var menu = new FakeLongDefeatMenu();
        container.RegisterInstance<ILongDefeatMenuAdapter>(menu, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        var module = new LongDefeatModule();

        foreach (var phase in new[] { ApplyPhase.ProcessLoad, ApplyPhase.GameInit, ApplyPhase.FirstMission })
            module.OnPhase(phase, container);

        Assert.AreEqual(0, menu.AddCalls);
    }

    [TestMethod]
    public void RegisterServices_TheMenuEntryIsASingleton()
    {
        var container = Registered();
        container.RegisterInstance(Substitute.For<TAOM.Features.CoopInterop.IDedicatedServerProvider>());

        Assert.AreSame(container.Resolve<LongDefeatMenuEntry>(), container.Resolve<LongDefeatMenuEntry>());
    }
}
